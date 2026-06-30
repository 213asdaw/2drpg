package com.skeboss.modelengine;

import org.bukkit.Bukkit;
import org.bukkit.entity.Entity;
import org.bukkit.plugin.Plugin;

import java.lang.reflect.Method;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;
import java.util.function.IntConsumer;
import java.util.logging.Level;

/**
 * ModelEngine R3/R4 런타임 연동 (컴파일 시 JAR 불필요).
 */
public final class ModelEngineBridge {

    private final Plugin plugin;
    private boolean available;

    private Method getOrCreateModeledEntity;
    private Method createModeledEntity;
    private Method getModeledEntity;
    private Method createActiveModel;
    private Method createActiveModelFromBlueprint;
    private Method getBlueprint;
    private Class<?> activeModelClass;

    public ModelEngineBridge(Plugin plugin) {
        this.plugin = plugin;
        init();
    }

    private void init() {
        try {
            Class<?> apiClass = Class.forName("com.ticxo.modelengine.api.ModelEngineAPI");

            activeModelClass = Class.forName("com.ticxo.modelengine.api.model.ActiveModel");

            getOrCreateModeledEntity = findStaticMethod(apiClass, "getOrCreateModeledEntity", Entity.class);
            createModeledEntity = findStaticMethod(apiClass, "createModeledEntity", Entity.class);
            getModeledEntity = findStaticMethod(apiClass, "getModeledEntity", Entity.class);
            createActiveModel = findStaticMethod(apiClass, "createActiveModel", String.class);
            getBlueprint = findStaticMethod(apiClass, "getBlueprint", String.class);

            if (getBlueprint != null) {
                createActiveModelFromBlueprint = findStaticMethod(apiClass, "createActiveModel", Class.forName(
                        "com.ticxo.modelengine.api.generator.blueprint.ModelBlueprint"));
            }

            if (createActiveModel == null) {
                throw new NoSuchMethodException("createActiveModel(String)");
            }
            if (getOrCreateModeledEntity == null && createModeledEntity == null) {
                throw new NoSuchMethodException("getOrCreateModeledEntity / createModeledEntity");
            }

            available = true;
            plugin.getLogger().info("ModelEngine API 연결 완료");
        } catch (ReflectiveOperationException ex) {
            available = false;
            plugin.getLogger().log(Level.SEVERE, "ModelEngine API를 찾을 수 없습니다.", ex);
        }
    }

    public boolean isAvailable() {
        return available;
    }

    public BossModel attachModel(Entity entity, String modelId, boolean hideBaseEntity, double modelScale, double hitboxScale) {
        String resolvedId = resolveFirstAvailableModelId(modelId, List.of());
        return attachModelResolved(entity, resolvedId, hideBaseEntity, modelScale, hitboxScale);
    }

    /** player limb 모델 ID 자동 탐색 후 적용 (렌더러 즉시 초기화, 스킨은 이후 덮어씀) */
    public BossModel attachMinionModel(Entity entity, String modelId, List<String> fallbackIds,
                                       double modelScale, double hitboxScale) {
        String resolvedId = resolveAvailableModelId(modelId, fallbackIds);
        if (resolvedId == null) {
            throw new IllegalStateException("잡몹 blueprint 없음 — /skeboss minion check");
        }
        if (!resolvedId.equals(modelId)) {
            plugin.getLogger().info("잡몹 모델 ID 폴백: " + modelId + " → " + resolvedId);
        }
        return attachModelResolved(entity, resolvedId, false, modelScale, hitboxScale, false);
    }

    /** blueprint가 실제로 있는 모델 ID만 반환, 없으면 null */
    public String resolveAvailableModelId(String primaryId, List<String> fallbackIds) {
        Set<String> candidates = new LinkedHashSet<>();
        if (primaryId != null && !primaryId.isBlank()) {
            candidates.add(primaryId);
        }
        if (fallbackIds != null) {
            candidates.addAll(fallbackIds);
        }
        for (String id : candidates) {
            if (blueprintExists(id)) {
                return id;
            }
        }
        return null;
    }

    public String resolveFirstAvailableModelId(String primaryId, List<String> fallbackIds) {
        String available = resolveAvailableModelId(primaryId, fallbackIds);
        if (available != null) {
            return available;
        }
        return primaryId != null ? primaryId : "skin";
    }

    public boolean hasBlueprint(String modelId) {
        return blueprintExists(modelId);
    }

    private boolean blueprintExists(String modelId) {
        if (getBlueprint == null || modelId == null || modelId.isBlank()) {
            return false;
        }
        try {
            return getBlueprint.invoke(null, modelId) != null;
        } catch (ReflectiveOperationException ex) {
            return false;
        }
    }

    private BossModel attachModelResolved(Entity entity, String modelId, boolean hideBaseEntity,
                                          double modelScale, double hitboxScale) {
        return attachModelResolved(entity, modelId, hideBaseEntity, modelScale, hitboxScale, false);
    }

    private BossModel attachModelResolved(Entity entity, String modelId, boolean hideBaseEntity,
                                          double modelScale, double hitboxScale, boolean deferRendererInit) {
        if (!available) {
            throw new IllegalStateException("ModelEngine 사용 불가");
        }

        try {
            Object modeledEntity = createModeledEntityWrapper(entity);
            Object activeModel = createActiveModelInstance(modelId);
            if (activeModel == null) {
                throw new IllegalStateException("모델 ID 없음: " + modelId + " (/meg reload 후 blueprint 확인)");
            }

            addModel(modeledEntity, activeModel);
            applyScale(activeModel, modelScale, hitboxScale);
            tryInvoke(activeModel, "setLockYaw", new Class<?>[]{boolean.class}, false);
            tryInvoke(activeModel, "setModelRotationLocked", new Class<?>[]{Boolean.class}, false);

            // player limb 잡몹: 본 생성은 즉시, 렌더러/스킨은 스킨 적용 후 초기화
            invokeOptional(activeModel, "generateModel");

            if (!deferRendererInit) {
                invokeOptional(activeModel, "initializeRenderer");
                syncNearbyPlayers(modeledEntity, activeModel, entity, 64.0, false);
            }

            if (hideBaseEntity && !deferRendererInit) {
                // 모델이 클라이언트에 뜬 뒤 본체 숨김 (즉시 숨기면 투명해질 수 있음)
                Bukkit.getScheduler().runTaskLater(plugin, () -> {
                    if (!entity.isValid() || entity.isDead()) {
                        return;
                    }
                    tryInvoke(modeledEntity, "setBaseEntityVisible", new Class<?>[]{boolean.class}, false);
                    syncNearbyPlayers(modeledEntity, activeModel, entity, 64.0, false);
                }, 2L);
            }

            plugin.getLogger().info("ModelEngine 모델 적용: " + modelId + " → " + entity.getUniqueId());
            return new BossModel(modeledEntity, activeModel);
        } catch (ReflectiveOperationException ex) {
            plugin.getLogger().log(Level.SEVERE, "ModelEngine attachModel 실패", ex);
            throw new IllegalStateException("ModelEngine 모델 적용 실패: " + rootMessage(ex), ex);
        }
    }

    public void syncNearbyPlayers(BossModel model, Entity entity, double radius) {
        syncNearbyPlayers(model.modeledEntity(), model.activeModel(), entity, radius, false);
    }

    /** 클라이언트에 player limb 모델을 다시 밀어 넣음 */
    public void forceResyncNearbyPlayers(BossModel model, Entity entity, double radius) {
        syncNearbyPlayers(model.modeledEntity(), model.activeModel(), entity, radius, true);
    }

    private void syncNearbyPlayers(Object modeledEntity, Object activeModel, Entity entity, double radius,
                                   boolean forceResync) {
        Object rangeManager = resolveRangeManager(modeledEntity);
        if (rangeManager != null) {
            syncNearbyPlayersWithRangeManager(rangeManager, entity, radius, forceResync);
            return;
        }
        if (syncNearbyPlayersModern(modeledEntity, activeModel, entity, radius, forceResync)) {
            return;
        }
        plugin.getLogger().warning("모델 시청자 동기화 실패 — ModelEngine R3/R4 API를 찾지 못했습니다.");
    }

    private void syncNearbyPlayersWithRangeManager(Object rangeManager, Entity entity, double radius,
                                                   boolean forceResync) {
        double radiusSq = radius * radius;
        for (org.bukkit.entity.Player player : entity.getWorld().getPlayers()) {
            if (!player.isValid() || player.getLocation().distanceSquared(entity.getLocation()) > radiusSq) {
                continue;
            }
            if (forceResync) {
                tryInvoke(rangeManager, "removePlayer", new Class<?>[]{org.bukkit.entity.Player.class}, player);
            }
            tryInvoke(rangeManager, "forceSpawn", new Class<?>[]{org.bukkit.entity.Player.class}, player);
            tryInvoke(rangeManager, "updatePlayer", new Class<?>[]{org.bukkit.entity.Player.class}, player);
            tryInvoke(rangeManager, "addPlayer", new Class<?>[]{org.bukkit.entity.Player.class}, player);
        }
    }

    /** R3: ModeledEntity/BaseEntity RangeManager. R4에서는 null. */
    private Object resolveRangeManager(Object modeledEntity) {
        Object rangeManager = invokeOptional(modeledEntity, "getRangeManager");
        if (rangeManager != null) {
            return rangeManager;
        }
        Object base = invokeOptional(modeledEntity, "getBase");
        if (base == null) {
            return null;
        }
        rangeManager = invokeOptional(base, "getRangeManager");
        if (rangeManager != null) {
            return rangeManager;
        }
        return invokeOptional(base, "wrapRangeManager", modeledEntity);
    }

    /** ModelEngine R4: RangeManager 대신 renderer resync 사용 (등록은 finalize 시 1회만) */
    private boolean syncNearbyPlayersModern(Object modeledEntity, Object activeModel, Entity entity, double radius,
                                            boolean forceResync) {
        boolean synced = false;
        Object base = invokeOptional(modeledEntity, "getBase");
        if (base != null) {
            int renderRadius = Math.max(16, (int) Math.ceil(radius));
            synced |= tryInvoke(base, "setRenderRadius", new Class<?>[]{int.class}, renderRadius);
        }

        invokeOptional(modeledEntity, "tick");
        if (activeModel != null) {
            invokeOptional(activeModel, "tick");
            Object renderer = invokeOptional(activeModel, "getModelRenderer");
            if (renderer != null) {
                tryInvoke(renderer, "pollFirstSpawn", new Class<?>[]{});
                synced = true;
                double radiusSq = radius * radius;
                for (org.bukkit.entity.Player player : entity.getWorld().getPlayers()) {
                    if (!player.isValid()
                            || player.getLocation().distanceSquared(entity.getLocation()) > radiusSq) {
                        continue;
                    }
                    UUID playerId = player.getUniqueId();
                    if (forceResync) {
                        synced |= tryInvoke(renderer, "pushFullUpdate", new Class<?>[]{UUID.class}, playerId);
                    }
                    synced |= tryInvoke(renderer, "pollFullUpdate", new Class<?>[]{UUID.class}, playerId);
                }
            }
        }
        return synced;
    }

    private Object createActiveModelInstance(String modelId) throws ReflectiveOperationException {
        if (getBlueprint != null && createActiveModelFromBlueprint != null) {
            Object blueprint = getBlueprint.invoke(null, modelId);
            if (blueprint != null) {
                Object fromBlueprint = createActiveModelFromBlueprint.invoke(null, blueprint);
                if (fromBlueprint != null) {
                    return fromBlueprint;
                }
            }
        }
        return createActiveModel.invoke(null, modelId);
    }

    private void applyScale(Object activeModel, double modelScale, double hitboxScale) {
        if (modelScale > 0 && modelScale != 1.0) {
            if (!tryInvoke(activeModel, "setScale", new Class<?>[]{double.class}, modelScale)) {
                tryInvoke(activeModel, "setModelScale", new Class<?>[]{int.class}, (int) Math.round(modelScale));
            }
        }
        if (hitboxScale > 0 && hitboxScale != 1.0) {
            tryInvoke(activeModel, "setHitboxScale", new Class<?>[]{double.class}, hitboxScale);
        }
    }

    private void addModel(Object modeledEntity, Object activeModel) {
        if (tryInvoke(modeledEntity, "addModel",
                new Class<?>[]{activeModelClass, boolean.class}, activeModel, true)) {
            return;
        }
        if (tryInvoke(modeledEntity, "addModel",
                new Class<?>[]{activeModelClass}, activeModel)) {
            return;
        }
        throw new IllegalStateException("addModel 실패");
    }

    private Object createModeledEntityWrapper(Entity entity) throws ReflectiveOperationException {
        if (getOrCreateModeledEntity != null) {
            return getOrCreateModeledEntity.invoke(null, entity);
        }
        return createModeledEntity.invoke(null, entity);
    }

    public void syncBodyRotation(BossModel model, float yaw) {
        if (model == null) {
            return;
        }
        Object activeModel = model.activeModel();
        tryInvoke(activeModel, "setLockYaw", new Class<?>[]{boolean.class}, false);
        tryInvoke(activeModel, "setModelRotationLocked", new Class<?>[]{Boolean.class}, false);
        tryInvoke(activeModel, "setYBodyRot", new Class<?>[]{float.class}, yaw);
        tryInvoke(activeModel, "setYHeadRot", new Class<?>[]{float.class}, yaw);
    }

    public void destroy(BossModel model) {
        if (model == null || model.modeledEntity() == null) {
            return;
        }
        try {
            invokeFirst(model.modeledEntity(), "destroy");
        } catch (Exception ex) {
            plugin.getLogger().log(Level.WARNING, "ModeledEntity destroy 실패", ex);
        }
    }

    /** 모델 로드 실패 시 좀비 본체가 다시 보이도록 복구 */
    public void restoreBaseEntityVisibility(Entity entity) {
        entity.setInvisible(false);
        if (!available || getModeledEntity == null) {
            return;
        }
        try {
            Object modeledEntity = getModeledEntity.invoke(null, entity);
            if (modeledEntity != null) {
                tryInvoke(modeledEntity, "setBaseEntityVisible", new Class<?>[]{boolean.class}, true);
            }
        } catch (ReflectiveOperationException ex) {
            plugin.getLogger().log(Level.FINE, "restoreBaseEntityVisibility", ex);
        }
    }

    public void setBaseEntityVisible(BossModel model, Entity entity, boolean visible, double syncRadius) {
        if (model == null) {
            return;
        }
        tryInvoke(model.modeledEntity(), "setBaseEntityVisible", new Class<?>[]{boolean.class}, visible);
        syncNearbyPlayers(model, entity, syncRadius);
    }

    public int countPlayerLimbs(BossModel model) {
        if (model == null) {
            return 0;
        }
        return countPlayerLimbsOnActiveModel(model.activeModel());
    }

    /** ModelEngine PlayerLimb 본에 마인크래프트 유저 스킨 적용 (EMP4348 등) */
    public void applyPlayerSkin(BossModel model, Entity entity, String username, double syncRadius) {
        applyPlayerSkin(model, entity, username, syncRadius, null);
    }

    /** 스킨 적용 후 PlayerLimb 개수를 콜백으로 전달 (0이면 좀비 유지용) */
    public void applyPlayerSkin(BossModel model, Entity entity, String username, double syncRadius,
                                IntConsumer onComplete) {
        if (model == null || username == null || username.isBlank()) {
            if (onComplete != null) {
                onComplete.accept(0);
            }
            return;
        }
        plugin.getLogger().info("잡몹 스킨 조회 시작: " + username);
        Bukkit.getScheduler().runTaskAsynchronously(plugin, () -> {
            UUID uuid = null;
            String textures = null;
            try {
                uuid = resolveUsernameUuid(username);
                textures = fetchTexturesAsync(username);
            } catch (Exception ex) {
                plugin.getLogger().log(Level.WARNING, "잡몹 스킨 비동기 조회 예외: " + username, ex);
            }
            final UUID resolvedUuid = uuid;
            final String resolvedTextures = textures;
            Bukkit.getScheduler().runTask(plugin, () -> applyPlayerSkinOnMainThread(
                    model, entity, username, syncRadius, resolvedUuid, resolvedTextures, onComplete));
        });
    }

    public UUID lookupUsernameUuid(String username) {
        return resolveUsernameUuid(username);
    }

    public String lookupTexturesAsync(String username) {
        return fetchTexturesAsync(username);
    }

    /** 스킨 조회만 테스트 (명령어 진단용) */
    public SkinLookupResult testSkinLookup(String username) {
        if (username == null || username.isBlank()) {
            return new SkinLookupResult(null, null, "닉네임이 비어 있습니다.");
        }
        UUID uuid = resolveUsernameUuid(username);
        if (uuid == null) {
            return new SkinLookupResult(null, null, "UUID 조회 실패 — 닉네임이 실제 마인크래프트 계정인지 확인하세요.");
        }
        String textures = fetchTexturesOnMainThread(username, uuid);
        if (textures == null || textures.isBlank()) {
            return new SkinLookupResult(uuid, null, "텍스처 조회 실패 — Mojang API / 인터넷 연결을 확인하세요.");
        }
        return new SkinLookupResult(uuid, textures, null);
    }

    public record SkinLookupResult(UUID uuid, String textures, String error) {
        public boolean success() {
            return error == null && uuid != null && textures != null && !textures.isBlank();
        }
    }

    private void applyPlayerSkinOnMainThread(BossModel model, Entity entity, String username, double syncRadius,
                                             UUID uuid, String textures, IntConsumer onComplete) {
        int limbs = 0;
        try {
            if (!entity.isValid() || entity.isDead()) {
                plugin.getLogger().warning("잡몹 스킨 적용 중단 — 엔티티가 없습니다: " + username);
                return;
            }

            if (uuid == null) {
                uuid = resolveUsernameUuid(username);
            }
            if (textures == null || textures.isBlank()) {
                textures = fetchTexturesOnMainThread(username, uuid);
            }
            if (uuid == null || textures == null || textures.isBlank()) {
                plugin.getLogger().warning("스킨 조회 실패: " + username
                        + " — /skeboss minion skin-test 로 진단, 닉네임·인터넷 확인");
                ensureMinionRenderer(model, entity, syncRadius, "스킨 조회 실패");
                return;
            }

            Object profile = resolveSkinProfile(username, uuid, textures);
            if (profile == null) {
                plugin.getLogger().warning("프로필 생성 실패: " + username);
                ensureMinionRenderer(model, entity, syncRadius, "프로필 생성 실패");
                return;
            }

            warmupUserLimbRegistry(username, uuid, textures);
            limbs = finalizePlayerLimbModel(model, entity, syncRadius, username, profile);
            if (limbs > 0) {
                plugin.getLogger().info("잡몹 스킨 적용: " + username + " (PlayerLimb " + limbs + "개)");
            } else {
                int detected = countPlayerLimbsOnActiveModel(model.activeModel());
                if (detected > 0) {
                    plugin.getLogger().warning("PlayerLimb " + detected
                            + "개 있으나 setTexture 실패 — " + username + " 스킨 미적용");
                } else {
                    plugin.getLogger().warning("PlayerLimb 본 없음 — /meg reload models 후 player_model 확인");
                }
                ensureMinionRenderer(model, entity, syncRadius, "setTexture 실패");
            }
        } catch (Exception ex) {
            plugin.getLogger().log(Level.SEVERE, "잡몹 스킨 적용 중 예외: " + username, ex);
            ensureMinionRenderer(model, entity, syncRadius, "예외");
        } finally {
            if (onComplete != null) {
                onComplete.accept(limbs);
            }
        }
    }

    private Object resolveSkinProfile(String username, UUID uuid, String textures) {
        Object paperProfile = buildPaperProfile(uuid, username, textures);
        if (paperProfile != null) {
            return paperProfile;
        }
        Object mojangProfile = fetchMojangProfile(username);
        if (mojangProfile != null) {
            return mojangProfile;
        }
        return fetchProfileOnMainThread(username, uuid);
    }

    private Object fetchProfileOnMainThread(String username, UUID uuid) {
        if (uuid == null) {
            uuid = resolveUsernameUuid(username);
        }
        if (uuid == null) {
            return null;
        }
        try {
            Object profile = Bukkit.createProfile(uuid, username);
            if (tryInvoke(profile, "complete", new Class<?>[]{boolean.class}, true)) {
                return profile;
            }
        } catch (Exception ex) {
            plugin.getLogger().log(Level.WARNING, "Bukkit profile.complete 실패: " + username, ex);
        }
        return fetchMojangProfile(username);
    }

    /** 비동기 스레드에서 호출 가능 — MojangAPI만 사용 */
    private String fetchTexturesAsync(String username) {
        Object mojangProfile = fetchMojangProfile(username);
        if (mojangProfile != null) {
            return extractTexturesProperty(mojangProfile);
        }
        return null;
    }

    /** 메인 스레드 전용 — Paper profile.complete + MojangAPI 폴백 */
    private String fetchTexturesOnMainThread(String username, UUID uuid) {
        if (uuid != null) {
            try {
                Object profile = Bukkit.createProfile(uuid, username);
                if (tryInvoke(profile, "complete", new Class<?>[]{boolean.class}, true)) {
                    String textures = extractTexturesProperty(profile);
                    if (textures != null) {
                        plugin.getLogger().info("Bukkit 프로필 스킨 로드: " + username);
                        return textures;
                    }
                }
            } catch (Exception ex) {
                plugin.getLogger().log(Level.WARNING, "Bukkit profile 조회 실패: " + username, ex);
            }
        }
        Object mojangProfile = fetchMojangProfile(username);
        if (mojangProfile != null) {
            String textures = extractTexturesProperty(mojangProfile);
            if (textures != null) {
                plugin.getLogger().info("MojangAPI 스킨 로드: " + username);
                return textures;
            }
        }
        return null;
    }

    private void ensureMinionRenderer(BossModel model, Entity entity, double syncRadius, String reason) {
        try {
            Object activeModel = model.activeModel();
            invokeOptional(activeModel, "initializeRenderer");
            for (Object bone : activeModelBones(activeModel).values()) {
                tryInvoke(bone, "setVisible", new Class<?>[]{boolean.class}, true);
            }
            invokeOptional(model.modeledEntity(), "tick");
            invokeOptional(activeModel, "tick");
            forceResyncNearbyPlayers(model, entity, syncRadius);
            plugin.getLogger().info("잡몹 렌더러 동기화(폴백): " + reason);
        } catch (Exception ex) {
            plugin.getLogger().log(Level.WARNING, "잡몹 렌더러 폴백 실패: " + reason, ex);
        }
    }

    private Object buildPaperProfile(UUID uuid, String username, String textures) {
        try {
            Object profile = Bukkit.createProfile(uuid, username);
            if (addTexturesProperty(profile, textures)) {
                return profile;
            }
            if (tryInvoke(profile, "setProperty",
                    new Class<?>[]{String.class, String.class, String.class},
                    "textures", textures, null)) {
                return profile;
            }
            return toPaperProfile(username, uuid, textures);
        } catch (Exception ex) {
            plugin.getLogger().log(Level.WARNING, "Paper 프로필 빌드 실패: " + username, ex);
            return null;
        }
    }

    private boolean addTexturesProperty(Object profile, String textures) {
        try {
            Object properties = invokeOptional(profile, "getProperties");
            if (properties == null) {
                return false;
            }
            for (String propertyClassName : List.of(
                    "com.destroystokyo.paper.profile.ProfileProperty",
                    "org.bukkit.profile.PlayerProfile$Property")) {
                try {
                    Class<?> propertyClass = Class.forName(propertyClassName);
                    Object property = propertyClass.getConstructor(String.class, String.class, String.class)
                            .newInstance("textures", textures, null);
                    if (tryInvoke(properties, "add", new Class<?>[]{propertyClass}, property)) {
                        return true;
                    }
                    if (tryInvoke(properties, "add", new Class<?>[]{Object.class}, property)) {
                        return true;
                    }
                } catch (ReflectiveOperationException ignored) {
                }
            }
        } catch (Exception ex) {
            plugin.getLogger().log(Level.FINE, "ProfileProperty 추가 실패", ex);
        }
        return false;
    }

    private Object toPaperProfile(String username, UUID uuid, String textures) {
        if (uuid == null || textures == null) {
            return null;
        }
        try {
            Object profile = Bukkit.createProfile(uuid, username);
            if (addTexturesProperty(profile, textures)) {
                return profile;
            }
            Object properties = invokeOptional(profile, "getProperties");
            if (properties != null) {
                invokeOptional(properties, "put", "textures", textures);
            }
            return profile;
        } catch (Exception ex) {
            plugin.getLogger().log(Level.FINE, "Paper profile 변환 실패: " + username, ex);
            return null;
        }
    }

    private Object fetchMojangProfile(String username) {
        try {
            Class<?> mojangApi = Class.forName("com.ticxo.modelengine.api.utils.MojangAPI");
            UUID uuid = (UUID) mojangApi.getMethod("getUUIDFromUsername", String.class).invoke(null, username);
            if (uuid == null) {
                return null;
            }
            return mojangApi.getMethod("fromUUID", UUID.class).invoke(null, uuid);
        } catch (ReflectiveOperationException ex) {
            plugin.getLogger().log(Level.WARNING, "MojangAPI 스킨 조회 실패: " + username, ex);
            return null;
        }
    }

    private Object fetchBukkitProfile(String username) {
        UUID uuid = resolveUsernameUuid(username);
        if (uuid == null) {
            return null;
        }
        try {
            Object profile = Bukkit.class.getMethod("createProfile", UUID.class, String.class)
                    .invoke(null, uuid, username);
            if (tryInvoke(profile, "complete", new Class<?>[]{boolean.class}, true)) {
                plugin.getLogger().info("Bukkit PlayerProfile 스킨 로드: " + username);
                return profile;
            }
        } catch (ReflectiveOperationException ex) {
            plugin.getLogger().log(Level.FINE, "Bukkit profile 실패: " + username, ex);
        }
        return null;
    }

    private UUID resolveUsernameUuid(String username) {
        try {
            Class<?> mojangApi = Class.forName("com.ticxo.modelengine.api.utils.MojangAPI");
            return (UUID) mojangApi.getMethod("getUUIDFromUsername", String.class).invoke(null, username);
        } catch (ReflectiveOperationException ex) {
            return null;
        }
    }

    private int applySkinToPlayerLimbs(Object activeModel, String username, Object profile) {
        org.bukkit.entity.Player online = Bukkit.getPlayerExact(username);
        if (online != null) {
            int fromPlayer = applyPlayerToPlayerLimbs(activeModel, online);
            if (fromPlayer > 0) {
                return fromPlayer;
            }
        }
        return applyProfileToPlayerLimbs(activeModel, profile);
    }

    private int applyPlayerToPlayerLimbs(Object activeModel, org.bukkit.entity.Player player) {
        Class<?> playerLimbClass = playerLimbClass();
        if (playerLimbClass == null) {
            return 0;
        }
        int applied = 0;
        for (Object bone : activeModelBones(activeModel).values()) {
            for (Object behavior : iterateBoneBehaviors(bone)) {
                if (!playerLimbClass.isInstance(behavior)) {
                    continue;
                }
                if (tryInvoke(behavior, "setTexture",
                        new Class<?>[]{org.bukkit.entity.Player.class}, player)) {
                    applied++;
                }
            }
        }
        return applied;
    }

    private int applyProfileToPlayerLimbs(Object activeModel, Object profile) {
        Class<?> playerLimbClass = playerLimbClass();
        if (playerLimbClass == null) {
            return 0;
        }

        Map<String, Object> bones = activeModelBones(activeModel);
        if (bones.isEmpty()) {
            return 0;
        }

        int applied = 0;
        for (Object bone : bones.values()) {
            for (Object behavior : iterateBoneBehaviors(bone)) {
                if (!playerLimbClass.isInstance(behavior)) {
                    continue;
                }
                if (applyTextureToPlayerLimb(behavior, profile)) {
                    applied++;
                }
            }
        }
        return applied;
    }

    private int countPlayerLimbsOnActiveModel(Object activeModel) {
        Class<?> playerLimbClass = playerLimbClass();
        if (playerLimbClass == null) {
            return 0;
        }

        int count = 0;
        for (Object bone : activeModelBones(activeModel).values()) {
            for (Object behavior : iterateBoneBehaviors(bone)) {
                if (playerLimbClass.isInstance(behavior)) {
                    count++;
                }
            }
        }
        return count;
    }

    private Class<?> playerLimbClass() {
        try {
            return Class.forName("com.ticxo.modelengine.api.model.bone.type.PlayerLimb");
        } catch (ClassNotFoundException ex) {
            return null;
        }
    }

    private void ensureModelBonesReady(Object activeModel) {
        if (!activeModelBones(activeModel).isEmpty()) {
            return;
        }
        invokeOptional(activeModel, "generateModel");
    }

    @SuppressWarnings("unchecked")
    private Map<String, Object> activeModelBones(Object activeModel) {
        Object bonesMap = unwrapOptional(invokeOptional(activeModel, "getBones"));
        if (bonesMap instanceof Map<?, ?> bones) {
            return (Map<String, Object>) bones;
        }
        return Map.of();
    }

    private Object unwrapOptional(Object value) {
        if (value == null) {
            return null;
        }
        if (!"java.util.Optional".equals(value.getClass().getName())) {
            return value;
        }
        try {
            Method isPresent = value.getClass().getMethod("isPresent");
            Method get = value.getClass().getMethod("get");
            if (Boolean.TRUE.equals(isPresent.invoke(value))) {
                return get.invoke(value);
            }
        } catch (ReflectiveOperationException ex) {
            plugin.getLogger().log(Level.FINE, "Optional unwrap 실패", ex);
        }
        return null;
    }

    private List<Object> iterateBoneBehaviors(Object bone) {
        Object behaviors = invokeOptional(bone, "getImmutableBoneBehaviors");
        if (behaviors instanceof Map<?, ?> map) {
            return new ArrayList<>(map.values());
        }
        if (behaviors instanceof Iterable<?> iterable) {
            List<Object> list = new ArrayList<>();
            for (Object behavior : iterable) {
                list.add(behavior);
            }
            return list;
        }
        return List.of();
    }

    private boolean applyTextureToPlayerLimb(Object playerLimb, Object profile) {
        org.bukkit.entity.Player player = profile instanceof org.bukkit.entity.Player p ? p : null;
        if (player != null) {
            if (tryInvoke(playerLimb, "setTexture",
                    new Class<?>[]{org.bukkit.entity.Player.class}, player)) {
                return true;
            }
        }

        for (Object candidate : List.of(profile, toPaperProfileForTexture(profile))) {
            if (candidate == null) {
                continue;
            }
            if (invokeSetTexture(playerLimb, candidate)) {
                return true;
            }
        }
        return false;
    }

    private boolean invokeSetTexture(Object playerLimb, Object textureSource) {
        Method match = findMethodByNameAndArity(playerLimb.getClass(), "setTexture", 1);
        if (match == null) {
            return false;
        }
        try {
            Object[] args = convertArgs(match.getParameterTypes(), new Object[]{textureSource});
            match.invoke(playerLimb, args);
            return true;
        } catch (ReflectiveOperationException ex) {
            plugin.getLogger().log(Level.FINE, "setTexture(" + textureSource.getClass().getSimpleName() + ") 실패", ex);
            return false;
        }
    }

    private Object toPaperProfileForTexture(Object profile) {
        if (profile == null) {
            return null;
        }
        for (String className : List.of(
                "com.destroystokyo.paper.profile.PlayerProfile",
                "org.bukkit.profile.PlayerProfile")) {
            try {
                if (Class.forName(className).isInstance(profile)) {
                    return profile;
                }
            } catch (ClassNotFoundException ignored) {
            }
        }
        return profile;
    }

    private void warmupUserLimbRegistry(String username, UUID uuid, String texturesValue) {
        try {
            Object registry = getUserLimbRegistry();
            if (registry == null) {
                plugin.getLogger().warning("UserLimbRegistry 없음 — player limb 스킨 캐시 불가");
                return;
            }

            invokeOptional(registry, "generateDefaults");

            org.bukkit.entity.Player online = Bukkit.getPlayerExact(username);
            if (online != null && tryInvoke(registry, "generate",
                    new Class<?>[]{org.bukkit.entity.Player.class}, online)) {
                return;
            }

            if (texturesValue == null) {
                return;
            }
            boolean slim = isSlimSkin(texturesValue);
            if (uuid != null) {
                tryInvoke(registry, "generate",
                        new Class<?>[]{String.class, String.class, boolean.class},
                        uuid.toString(), texturesValue, slim);
            }
            tryInvoke(registry, "generate",
                    new Class<?>[]{String.class, String.class, boolean.class},
                    username, texturesValue, slim);
        } catch (ReflectiveOperationException ex) {
            plugin.getLogger().log(Level.WARNING, "UserLimbRegistry warmup 실패: " + username, ex);
        }
    }

    private Object getUserLimbRegistry() throws ReflectiveOperationException {
        Class<?> apiClass = Class.forName("com.ticxo.modelengine.api.ModelEngineAPI");
        Method staticGetter = findStaticMethod(apiClass, "getUserLimbRegistry");
        if (staticGetter != null) {
            return staticGetter.invoke(null);
        }
        Method getApi = findStaticMethod(apiClass, "getAPI");
        if (getApi != null) {
            Object api = getApi.invoke(null);
            if (api != null) {
                return invokeOptional(api, "getUserLimbRegistry");
            }
        }
        return null;
    }

    /** 스킨 → initializeRenderer (ME 자동 등록, registerSelf 사용 안 함) */
    private int finalizePlayerLimbModel(BossModel model, Entity entity, double syncRadius,
                                        String username, Object profile) {
        Object activeModel = model.activeModel();
        Object modeledEntity = model.modeledEntity();

        int applied = applySkinToPlayerLimbs(activeModel, username, profile);

        for (Object bone : activeModelBones(activeModel).values()) {
            tryInvoke(bone, "setVisible", new Class<?>[]{boolean.class}, true);
        }
        try {
            invokeOptional(activeModel, "initializeRenderer");
            invokeOptional(modeledEntity, "tick");
            invokeOptional(activeModel, "tick");
            forceResyncNearbyPlayers(model, entity, syncRadius);
        } catch (Exception ex) {
            plugin.getLogger().log(Level.WARNING, "잡몹 렌더러 초기화/동기화 실패: " + username, ex);
        }
        Bukkit.getScheduler().runTaskLater(plugin, () -> {
            if (!entity.isValid() || entity.isDead()) {
                return;
            }
            forceResyncNearbyPlayers(model, entity, syncRadius);
        }, 5L);
        return applied;
    }

    private String extractTexturesProperty(Object profile) {
        Object textures = invokeOptional(profile, "getTextures");
        if (textures instanceof String string && !string.isBlank()) {
            return string;
        }
        Object properties = invokeOptional(profile, "getProperties");
        if (properties == null) {
            return null;
        }
        Object direct = invokeOptional(properties, "get", "textures");
        if (direct != null) {
            Object value = invokeOptional(direct, "getValue");
            if (value instanceof String string) {
                return string;
            }
        }
        if (properties instanceof Iterable<?> iterable) {
            for (Object property : iterable) {
                Object name = invokeOptional(property, "getName");
                if (!"textures".equals(name)) {
                    continue;
                }
                Object value = invokeOptional(property, "getValue");
                if (value instanceof String string) {
                    return string;
                }
            }
        }
        return null;
    }

    private boolean isSlimSkin(String texturesValue) {
        try {
            String decoded = new String(java.util.Base64.getDecoder().decode(texturesValue));
            return decoded.contains("\"slim\"");
        } catch (IllegalArgumentException ex) {
            return false;
        }
    }

    private Object invokeStaticOptional(Class<?> clazz, String method, Object... args) throws ReflectiveOperationException {
        Method match = findMethodByNameAndArity(clazz, method, args.length);
        if (match == null) {
            return null;
        }
        return match.invoke(null, convertArgs(match.getParameterTypes(), args));
    }

    public void playLoopAnimation(BossModel model, String animation, double blendIn, double blendOut) {
        Object handler = animationHandler(model);
        if (!tryPlayAnimation(handler, animation, blendIn, blendOut)) {
            throw new IllegalStateException("playAnimation 실패: " + animation);
        }
    }

    public void stopAnimation(BossModel model, String animation) {
        Object handler = animationHandler(model);
        // hold 애니메이션은 forceStop 이 더 확실
        if (!tryInvoke(handler, "forceStopAnimation", new Class<?>[]{String.class}, animation)) {
            tryInvoke(handler, "stopAnimation", new Class<?>[]{String.class}, animation);
        }
    }

    public int estimateDurationTicks(BossModel model, String animation, int fallbackTicks) {
        try {
            Object handler = animationHandler(model);
            Object property = invokeFirst(handler, "getAnimation", String.class, animation);
            if (property == null) {
                return fallbackTicks;
            }

            Object length = invokeFirst(property, "getLength");
            if (length instanceof Number number) {
                return Math.max(10, (int) Math.ceil(number.doubleValue() * 20.0));
            }
        } catch (Exception ex) {
            plugin.getLogger().fine("애니메이션 길이 추정 실패, config 값 사용: " + animation);
        }
        return fallbackTicks;
    }

    private boolean tryPlayAnimation(Object handler, String animation, double blendIn, double blendOut) {
        return tryInvoke(handler, "playAnimation",
                new Class<?>[]{String.class, double.class, double.class, double.class, boolean.class},
                animation, blendIn, blendOut, 1.0d, true)
                || tryInvoke(handler, "playAnimation",
                new Class<?>[]{String.class, float.class, float.class, float.class, boolean.class},
                animation, (float) blendIn, (float) blendOut, 1.0f, true);
    }

    private Object animationHandler(BossModel model) {
        Object handler = invokeFirst(model.activeModel(), "getAnimationHandler");
        if (handler == null) {
            throw new IllegalStateException("getAnimationHandler 반환 null");
        }
        return handler;
    }

    private static Method findStaticMethod(Class<?> clazz, String name, Class<?>... paramTypes) {
        try {
            return clazz.getMethod(name, paramTypes);
        } catch (NoSuchMethodException ignored) {
            return null;
        }
    }

    private boolean tryInvoke(Object target, String method, Class<?>[] paramTypes, Object... args) {
        try {
            Method m = findMethod(target.getClass(), method, paramTypes);
            if (m == null) {
                return false;
            }
            m.invoke(target, args);
            return true;
        } catch (ReflectiveOperationException ex) {
            plugin.getLogger().log(Level.FINE, "ModelEngine optional call failed: " + method, ex);
            return false;
        }
    }

    private Object invokeOptional(Object target, String method, Object... args) {
        try {
            return invokeFirst(target, method, args);
        } catch (IllegalStateException ex) {
            return null;
        }
    }

    private Object invokeFirst(Object target, String method, Object... args) {
        Method match = findMethodByNameAndArity(target.getClass(), method, args.length);
        if (match == null) {
            throw new IllegalStateException("ModelEngine 호출 실패: " + method + " (메서드 없음, 클래스="
                    + target.getClass().getName() + ")");
        }
        try {
            Object[] converted = convertArgs(match.getParameterTypes(), args);
            return match.invoke(target, converted);
        } catch (ReflectiveOperationException ex) {
            throw new IllegalStateException("ModelEngine 호출 실패: " + method + " — " + rootMessage(ex), ex);
        }
    }

    private static Method findMethod(Class<?> type, String name, Class<?>[] paramTypes) {
        Class<?> current = type;
        while (current != null) {
            try {
                return current.getMethod(name, paramTypes);
            } catch (NoSuchMethodException ignored) {
                current = current.getSuperclass();
            }
        }
        for (Class<?> iface : type.getInterfaces()) {
            try {
                return iface.getMethod(name, paramTypes);
            } catch (NoSuchMethodException ignored) {
                // continue
            }
        }
        return null;
    }

    private static Method findMethodByNameAndArity(Class<?> type, String name, int arity) {
        Class<?> current = type;
        while (current != null) {
            for (Method method : current.getMethods()) {
                if (method.getName().equals(name) && method.getParameterCount() == arity) {
                    return method;
                }
            }
            current = current.getSuperclass();
        }
        return null;
    }

    private static Object[] convertArgs(Class<?>[] paramTypes, Object[] args) {
        Object[] converted = new Object[args.length];
        for (int i = 0; i < args.length; i++) {
            converted[i] = convertArg(paramTypes[i], args[i]);
        }
        return converted;
    }

    private static Object convertArg(Class<?> paramType, Object arg) {
        if (arg == null) {
            return null;
        }
        if (paramType.isInstance(arg)) {
            return arg;
        }
        if (paramType == float.class || paramType == Float.class) {
            return ((Number) arg).floatValue();
        }
        if (paramType == double.class || paramType == Double.class) {
            return ((Number) arg).doubleValue();
        }
        if (paramType == int.class || paramType == Integer.class) {
            return ((Number) arg).intValue();
        }
        if (paramType == boolean.class || paramType == Boolean.class) {
            return (Boolean) arg;
        }
        return arg;
    }

    private static String rootMessage(Throwable ex) {
        Throwable cause = ex.getCause() != null ? ex.getCause() : ex;
        return cause.getClass().getSimpleName() + ": " + cause.getMessage();
    }

    public record BossModel(Object modeledEntity, Object activeModel) {
    }
}
