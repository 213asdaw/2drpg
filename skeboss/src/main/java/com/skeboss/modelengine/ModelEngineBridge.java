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

    /** player limb 모델 ID 자동 탐색 후 적용 (좀비는 스킨 확인 전까지 유지) */
    public BossModel attachMinionModel(Entity entity, String modelId, List<String> fallbackIds,
                                       double modelScale, double hitboxScale) {
        String resolvedId = resolveAvailableModelId(modelId, fallbackIds);
        if (resolvedId == null) {
            throw new IllegalStateException("잡몹 blueprint 없음 — /skeboss minion check");
        }
        if (!resolvedId.equals(modelId)) {
            plugin.getLogger().info("잡몹 모델 ID 폴백: " + modelId + " → " + resolvedId);
        }
        return attachModelResolved(entity, resolvedId, false, modelScale, hitboxScale, true);
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

            registerModeledEntityForTracking(modeledEntity, entity);

            if (!deferRendererInit) {
                invokeOptional(activeModel, "generateModel");
                invokeOptional(activeModel, "initializeRenderer");
                syncNearbyPlayers(modeledEntity, activeModel, entity, 64.0, false);
            }

            if (hideBaseEntity && !deferRendererInit) {
                tryInvoke(modeledEntity, "setBaseEntityVisible", new Class<?>[]{boolean.class}, false);
                syncNearbyPlayers(modeledEntity, activeModel, entity, 64.0, false);
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

    /** ModelEngine R4: RangeManager 대신 ModelUpdaters + renderer resync 사용 */
    private boolean syncNearbyPlayersModern(Object modeledEntity, Object activeModel, Entity entity, double radius,
                                            boolean forceResync) {
        boolean synced = registerModeledEntityForTracking(modeledEntity, entity);
        Object base = invokeOptional(modeledEntity, "getBase");
        if (base != null) {
            int renderRadius = Math.max(16, (int) Math.ceil(radius));
            synced |= tryInvoke(base, "setRenderRadius", new Class<?>[]{int.class}, renderRadius);
        }
        synced |= startDesyncMonitor(entity.getUniqueId());

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

    private boolean registerModeledEntityForTracking(Object modeledEntity, Entity entity) {
        if (tryInvoke(modeledEntity, "registerSelf", new Class<?>[]{})) {
            return true;
        }

        Object base = invokeOptional(modeledEntity, "getBase");
        if (base == null) {
            return false;
        }

        try {
            Class<?> apiClass = Class.forName("com.ticxo.modelengine.api.ModelEngineAPI");
            Object updaters = getModelUpdaters(apiClass);
            if (updaters != null) {
                Object registered = invokeOptional(updaters, "registerModeledEntity", base, modeledEntity);
                if (registered != null || modeledEntity.equals(invokeOptional(updaters, "getModeledEntity",
                        entity.getUniqueId()))) {
                    return true;
                }
            }

            Method registerStatic = findStaticMethod(apiClass, "registerModeledEntity",
                    Class.forName("com.ticxo.modelengine.api.entity.BaseEntity"),
                    Class.forName("com.ticxo.modelengine.api.model.ModeledEntity"));
            if (registerStatic != null) {
                registerStatic.invoke(null, base, modeledEntity);
                return true;
            }
        } catch (ReflectiveOperationException ex) {
            plugin.getLogger().log(Level.FINE, "ModelEngine 등록 실패: " + entity.getUniqueId(), ex);
        }
        return false;
    }

    private Object getModelUpdaters(Class<?> apiClass) throws ReflectiveOperationException {
        Method staticGetter = findStaticMethod(apiClass, "getModelUpdaters");
        if (staticGetter != null) {
            return staticGetter.invoke(null);
        }
        Method getApi = findStaticMethod(apiClass, "getAPI");
        if (getApi != null) {
            Object api = getApi.invoke(null);
            if (api != null) {
                return invokeOptional(api, "getModelUpdaters");
            }
        }
        return null;
    }

    private boolean startDesyncMonitor(UUID entityId) {
        try {
            Class<?> apiClass = Class.forName("com.ticxo.modelengine.api.ModelEngineAPI");
            Object updaters = getModelUpdaters(apiClass);
            if (updaters == null) {
                return false;
            }
            invokeOptional(updaters, "startDesyncMonitor", entityId);
            return true;
        } catch (ReflectiveOperationException ex) {
            plugin.getLogger().log(Level.FINE, "DesyncMonitor 시작 실패: " + entityId, ex);
            return false;
        }
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
        Bukkit.getScheduler().runTaskAsynchronously(plugin, () -> {
            Object mojangProfile = fetchMojangProfile(username);
            Object resolvedProfile = mojangProfile != null ? mojangProfile : fetchBukkitProfile(username);
            Bukkit.getScheduler().runTask(plugin, () -> {
                if (!entity.isValid() || entity.isDead()) {
                    return;
                }
                if (resolvedProfile == null) {
                    plugin.getLogger().warning("스킨 조회 실패: " + username
                            + " — 닉네임·인터넷 연결을 확인하세요.");
                    if (onComplete != null) {
                        onComplete.accept(0);
                    }
                    return;
                }
                warmupUserLimbRegistry(username, resolvedProfile);
                int limbs = applySkinToPlayerLimbs(model.activeModel(), username, resolvedProfile);
                if (limbs > 0) {
                    finalizePlayerLimbModel(model, entity, syncRadius);
                    plugin.getLogger().info("잡몹 스킨 적용: " + username + " (PlayerLimb " + limbs + "개)");
                } else {
                    plugin.getLogger().warning("PlayerLimb 본 없음 — model-id가 플레이어 림 모델이어야 합니다."
                            + " ModelEngine 기본 예시: skin (/meg models list)");
                }
                if (onComplete != null) {
                    onComplete.accept(limbs);
                }
            });
        });
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

    @SuppressWarnings("unchecked")
    private Map<String, Object> activeModelBones(Object activeModel) {
        Object bonesMap = invokeOptional(activeModel, "getBones");
        if (bonesMap instanceof Map<?, ?> bones) {
            return (Map<String, Object>) bones;
        }
        return Map.of();
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
        if (tryInvoke(playerLimb, "setTexture", new Class<?>[]{profile.getClass()}, profile)) {
            return true;
        }
        try {
            Class<?> paperProfile = Class.forName("com.destroystokyo.paper.profile.PlayerProfile");
            if (tryInvoke(playerLimb, "setTexture", new Class<?>[]{paperProfile}, profile)) {
                return true;
            }
        } catch (ClassNotFoundException ignored) {
        }
        if (profile instanceof org.bukkit.entity.Player player) {
            return tryInvoke(playerLimb, "setTexture",
                    new Class<?>[]{org.bukkit.entity.Player.class}, player);
        }
        return false;
    }

    private void warmupUserLimbRegistry(String username, Object profile) {
        try {
            Class<?> apiClass = Class.forName("com.ticxo.modelengine.api.ModelEngineAPI");
            Method getRegistry = apiClass.getMethod("getUserLimbRegistry");
            Object registry = getRegistry.invoke(null);
            if (registry == null) {
                return;
            }

            invokeOptional(registry, "generateDefaults");

            org.bukkit.entity.Player online = Bukkit.getPlayerExact(username);
            if (online != null && tryInvoke(registry, "generate",
                    new Class<?>[]{org.bukkit.entity.Player.class}, online)) {
                return;
            }

            String texturesValue = extractTexturesProperty(profile);
            if (texturesValue == null) {
                return;
            }
            boolean slim = isSlimSkin(texturesValue);
            UUID uuid = resolveUsernameUuid(username);
            if (uuid != null) {
                tryInvoke(registry, "generate",
                        new Class<?>[]{String.class, String.class, boolean.class},
                        uuid.toString(), texturesValue, slim);
            }
            tryInvoke(registry, "generate",
                    new Class<?>[]{String.class, String.class, boolean.class},
                    username, texturesValue, slim);
        } catch (ReflectiveOperationException ex) {
            plugin.getLogger().log(Level.FINE, "UserLimbRegistry warmup 실패: " + username, ex);
        }
    }

    private void finalizePlayerLimbModel(BossModel model, Entity entity, double syncRadius) {
        Object activeModel = model.activeModel();
        Object modeledEntity = model.modeledEntity();
        for (Object bone : activeModelBones(activeModel).values()) {
            tryInvoke(bone, "setVisible", new Class<?>[]{boolean.class}, true);
        }
        invokeOptional(activeModel, "generateModel");
        invokeOptional(activeModel, "initializeRenderer");
        invokeOptional(modeledEntity, "tick");
        invokeOptional(activeModel, "tick");
        forceResyncNearbyPlayers(model, entity, syncRadius);
        Bukkit.getScheduler().runTaskLater(plugin, () -> {
            if (!entity.isValid() || entity.isDead()) {
                return;
            }
            forceResyncNearbyPlayers(model, entity, syncRadius);
        }, 10L);
        Bukkit.getScheduler().runTaskLater(plugin, () -> {
            if (!entity.isValid() || entity.isDead()) {
                return;
            }
            forceResyncNearbyPlayers(model, entity, syncRadius);
        }, 40L);
    }

    private String extractTexturesProperty(Object profile) {
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
