package com.skeboss.modelengine;

import org.bukkit.entity.Entity;
import org.bukkit.plugin.Plugin;

import java.lang.reflect.Method;
import java.util.logging.Level;

/**
 * ModelEngine R3/R4 런타임 연동 (컴파일 시 JAR 불필요).
 */
public final class ModelEngineBridge {

    private final Plugin plugin;
    private boolean available;

    private Class<?> apiClass;
    private Method getOrCreateModeledEntity;
    private Method createActiveModel;

    public ModelEngineBridge(Plugin plugin) {
        this.plugin = plugin;
        init();
    }

    private void init() {
        try {
            apiClass = Class.forName("com.ticxo.modelengine.api.ModelEngineAPI");
            getOrCreateModeledEntity = apiClass.getMethod("getOrCreateModeledEntity", Entity.class);
            createActiveModel = apiClass.getMethod("createActiveModel", String.class);
            available = true;
            plugin.getLogger().info("ModelEngine API 연결 완료");
        } catch (ReflectiveOperationException ex) {
            available = false;
            plugin.getLogger().severe("ModelEngine API를 찾을 수 없습니다. ModelEngine 플러그인을 설치하세요.");
        }
    }

    public boolean isAvailable() {
        return available;
    }

    public BossModel attachModel(Entity entity, String modelId) {
        if (!available) {
            throw new IllegalStateException("ModelEngine 사용 불가");
        }

        try {
            Object modeledEntity = getOrCreateModeledEntity.invoke(null, entity);
            Object activeModel = createActiveModel.invoke(null, modelId);
            if (activeModel == null) {
                throw new IllegalStateException("모델 ID 없음: " + modelId);
            }

            invoke(modeledEntity, "addModel", activeModel, true);

            // 좀비 바닐라 모델 숨김 — 겹침 방지
            invoke(activeModel, "setBaseEntityVisible", false);
            invoke(modeledEntity, "setBaseEntityVisible", false);

            return new BossModel(modeledEntity, activeModel);
        } catch (ReflectiveOperationException ex) {
            throw new IllegalStateException("ModelEngine 모델 적용 실패: " + ex.getMessage(), ex);
        }
    }

    public void destroy(BossModel model) {
        if (model == null || model.modeledEntity() == null) {
            return;
        }
        try {
            invoke(model.modeledEntity(), "destroy");
        } catch (Exception ex) {
            plugin.getLogger().log(Level.WARNING, "ModeledEntity destroy 실패", ex);
        }
    }

    public void playLoopAnimation(BossModel model, String animation, double blendIn, double blendOut) {
        Object handler = animationHandler(model);
        invoke(handler, "playAnimation", animation, blendIn, blendOut, 1.0, true);
    }

    public void stopAnimation(BossModel model, String animation) {
        Object handler = animationHandler(model);
        invoke(handler, "stopAnimation", animation);
    }

    public int estimateDurationTicks(BossModel model, String animation, int fallbackTicks) {
        try {
            Object handler = animationHandler(model);
            Object anim = invoke(handler, "getAnimation", animation);
            if (anim == null) {
                return fallbackTicks;
            }
            Object length = invoke(anim, "getLength");
            if (length instanceof Number number) {
                return Math.max(10, (int) Math.ceil(number.doubleValue() * 20.0));
            }
        } catch (Exception ignored) {
            // API 차이 시 config 값 사용
        }
        return fallbackTicks;
    }

    private Object animationHandler(BossModel model) {
        return invoke(model.activeModel(), "getAnimationHandler");
    }

    private Object invoke(Object target, String method, Object... args) {
        try {
            Class<?>[] types = new Class<?>[args.length];
            for (int i = 0; i < args.length; i++) {
                types[i] = wrapPrimitive(args[i].getClass());
            }
            Method m = target.getClass().getMethod(method, types);
            return m.invoke(target, args);
        } catch (ReflectiveOperationException ex) {
            throw new IllegalStateException("ModelEngine 호출 실패: " + method, ex);
        }
    }

    private static Class<?> wrapPrimitive(Class<?> type) {
        if (type == Boolean.class) {
            return boolean.class;
        }
        if (type == Integer.class) {
            return int.class;
        }
        if (type == Double.class) {
            return double.class;
        }
        if (type == Float.class) {
            return float.class;
        }
        return type;
    }

    public record BossModel(Object modeledEntity, Object activeModel) {
    }
}
