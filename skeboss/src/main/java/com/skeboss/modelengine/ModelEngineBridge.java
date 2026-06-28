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
import java.util.logging.Level;

/**
 * ModelEngine R3/R4 런타임 연동 (컴파일 시 JAR 불필요).
 */
public final class ModelEngineBridge {

    private final Plugin plugin;
    private boolean available;

    private Method getOrCreateModeledEntity;
    private Method createModeledEntity;
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

    /** player limb 모델 ID 자동 탐색 후 적용 */
    public BossModel attachMinionModel(Entity entity, String modelId, List<String> fallbackIds,
                                       boolean hideBaseEntity, double modelScale, double hitboxScale) {
        String resolvedId = resolveFirstAvailableModelId(modelId, fallbackIds);
        if (!resolvedId.equals(modelId)) {
            plugin.getLogger().info("잡몹 모델 ID 폴백: " + modelId + " → " + resolvedId);
        }
        return attachModelResolved(entity, resolvedId, hideBaseEntity, modelScale, hitboxScale);
    }

    public String resolveFirstAvailableModelId(String primaryId, List<String> fallbackIds) {
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
        return primaryId != null ? primaryId : "skin";
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

            // 모델 먼저 플레이어에게 보이게 한 뒤 좀비 숨김
            syncNearbyPlayers(modeledEntity, entity, 64.0);

            if (hideBaseEntity) {
                tryInvoke(modeledEntity, "setBaseEntityVisible", new Class<?>[]{boolean.class}, false);
                syncNearbyPlayers(modeledEntity, entity, 64.0);
            }

            plugin.getLogger().info("ModelEngine 모델 적용: " + modelId + " → " + entity.getUniqueId());
            return new BossModel(modeledEntity, activeModel);
        } catch (ReflectiveOperationException ex) {
            plugin.getLogger().log(Level.SEVERE, "ModelEngine attachModel 실패", ex);
            throw new IllegalStateException("ModelEngine 모델 적용 실패: " + rootMessage(ex), ex);
        }
    }

    public void syncNearbyPlayers(BossModel model, Entity entity, double radius) {
        syncNearbyPlayers(model.modeledEntity(), entity, radius);
    }

    private void syncNearbyPlayers(Object modeledEntity, Entity entity, double radius) {
        Object rangeManager = invokeOptional(modeledEntity, "getRangeManager");
        if (rangeManager == null) {
            plugin.getLogger().warning("RangeManager 없음 — 모델이 안 보일 수 있습니다.");
            return;
        }

        double radiusSq = radius * radius;
        for (org.bukkit.entity.Player player : entity.getWorld().getPlayers()) {
            if (!player.isValid() || player.getLocation().distanceSquared(entity.getLocation()) > radiusSq) {
                continue;
            }
            tryInvoke(rangeManager, "forceSpawn", new Class<?>[]{org.bukkit.entity.Player.class}, player);
            tryInvoke(rangeManager, "updatePlayer", new Class<?>[]{org.bukkit.entity.Player.class}, player);
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

    /** ModelEngine PlayerLimb 본에 마인크래프트 유저 스킨 적용 (EMP4348 등) */
    public void applyPlayerSkin(BossModel model, Entity entity, String username, double syncRadius) {
        if (model == null || username == null || username.isBlank()) {
            return;
        }
        Bukkit.getScheduler().runTaskAsynchronously(plugin, () -> {
            Object profile = fetchMojangProfile(username);
            Bukkit.getScheduler().runTask(plugin, () -> {
                if (!entity.isValid() || entity.isDead()) {
                    return;
                }
                if (profile == null) {
                    plugin.getLogger().warning("스킨 조회 실패: " + username
                            + " — 닉네임·인터넷 연결을 확인하세요.");
                    return;
                }
                int limbs = applyProfileToPlayerLimbs(model.activeModel(), profile);
                if (limbs > 0) {
                    plugin.getLogger().info("잡몹 스킨 적용: " + username + " (PlayerLimb " + limbs + "개)");
                    syncNearbyPlayers(model, entity, syncRadius);
                } else {
                    plugin.getLogger().warning("PlayerLimb 본 없음 — model-id가 플레이어 림 모델이어야 합니다."
                            + " ModelEngine 기본 예시: skin (/meg models list)");
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

    private int applyProfileToPlayerLimbs(Object activeModel, Object profile) {
        Class<?> playerLimbClass;
        try {
            playerLimbClass = Class.forName("com.ticxo.modelengine.api.model.bone.type.PlayerLimb");
        } catch (ClassNotFoundException ex) {
            return 0;
        }

        Object bonesMap = invokeOptional(activeModel, "getBones");
        if (!(bonesMap instanceof Map<?, ?> bones)) {
            return 0;
        }

        int applied = 0;
        Class<?> profileType = profile.getClass();
        try {
            profileType = Class.forName("com.destroystokyo.paper.profile.PlayerProfile");
        } catch (ClassNotFoundException ignored) {
        }
        Class<?>[] textureParam = new Class<?>[]{profileType};
        for (Object bone : bones.values()) {
            Object behaviors = invokeOptional(bone, "getImmutableBoneBehaviors");
            if (!(behaviors instanceof Iterable<?> iterable)) {
                continue;
            }
            for (Object behavior : iterable) {
                if (!playerLimbClass.isInstance(behavior)) {
                    continue;
                }
                if (tryInvoke(behavior, "setTexture", textureParam, profile)) {
                    applied++;
                }
            }
        }
        return applied;
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
