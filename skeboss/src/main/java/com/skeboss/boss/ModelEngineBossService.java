package com.skeboss.boss;

import com.ticxo.modelengine.api.ModelEngineAPI;
import com.ticxo.modelengine.api.animation.handler.AnimationHandler;
import com.ticxo.modelengine.api.entity.ModeledEntity;
import com.ticxo.modelengine.api.model.ActiveModel;
import org.bukkit.Bukkit;
import org.bukkit.Location;
import org.bukkit.attribute.Attribute;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Mob;
import org.bukkit.entity.Zombie;
import org.bukkit.metadata.FixedMetadataValue;
import org.bukkit.plugin.Plugin;

import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

/**
 * ModelEngine 보스 생성/스킬/정리.
 *
 * <p>수정 포인트:
 * <ul>
 *   <li>좀비 겹침 → 바닐라 엔티티 숨김 + 무음/아기 해제</li>
 *   <li>몸 반대 → 스폰 yaw 보정 (config yaw-offset)</li>
 *   <li>스킬 후 복귀 → 스킬 길이 후 idle 강제 재생 + AI/중력 원복</li>
 * </ul>
 */
public final class ModelEngineBossService {

    public static final String METADATA_KEY = "skeboss";

    private final Plugin plugin;
    private final BossSettings settings;
    private final Map<UUID, SkeBoss> activeBosses = new ConcurrentHashMap<>();

    public ModelEngineBossService(Plugin plugin) {
        this.plugin = plugin;
        this.settings = new BossSettings((com.skeboss.SkeBossPlugin) plugin);
    }

    public SkeBoss spawn(Location location) {
        Location spawnLoc = location.clone();
        spawnLoc.setYaw(spawnLoc.getYaw() + settings.getYawOffset());

        Zombie zombie = location.getWorld().spawn(spawnLoc, Zombie.class, entity -> {
            entity.setBaby(false);
            entity.setSilent(true);
            entity.setCanPickupItems(false);
            entity.setRemoveWhenFarAway(false);
            entity.setShouldBurnInDay(false);
            entity.setCustomNameVisible(true);
            entity.setCustomName(colorize(settings.getDisplayName()));
            entity.setMetadata(METADATA_KEY, new FixedMetadataValue(plugin, true));

            if (entity.getAttribute(Attribute.GENERIC_MAX_HEALTH) != null) {
                entity.getAttribute(Attribute.GENERIC_MAX_HEALTH).setBaseValue(settings.getMaxHealth());
            }
            entity.setHealth(settings.getMaxHealth());
        });

        ModeledEntity modeledEntity = ModelEngineAPI.getOrCreateModeledEntity(zombie);
        ActiveModel activeModel = ModelEngineAPI.createActiveModel(settings.getModelId());
        if (activeModel == null) {
            zombie.remove();
            throw new IllegalStateException("모델을 찾을 수 없습니다: " + settings.getModelId()
                    + " (ModelEngine blueprints 확인)");
        }

        modeledEntity.addModel(activeModel, true);

        // [수정 1] 좀비 바닐라 모델 숨기기 — 겹침 방지
        activeModel.setBaseEntityVisible(false);
        modeledEntity.setBaseEntityVisible(false);

        playIdle(activeModel);

        SkeBoss boss = new SkeBoss(zombie, modeledEntity, activeModel, settings);
        activeBosses.put(zombie.getUniqueId(), boss);
        return boss;
    }

    public boolean castSkill(SkeBoss boss) {
        if (boss.isCastingSkill()) {
            return false;
        }

        ActiveModel model = boss.getActiveModel();
        AnimationHandler handler = model.getAnimationHandler();
        LivingEntity entity = boss.getEntity();

        boss.setCastingSkill(true);

        // 스킬 중 움직임 고정 (선택)
        if (entity instanceof Mob mob) {
            mob.setAI(false);
        }
        entity.setGravity(false);
        entity.setVelocity(entity.getVelocity().zero());

        double blendIn = settings.getBlendIn();
        double blendOut = settings.getBlendOut();
        handler.playAnimation(
                settings.getSkillAnimation(),
                blendIn,
                blendOut,
                1.0,
                true
        );

        int skillDurationTicks = estimateSkillDurationTicks(handler, settings.getSkillAnimation());

        Bukkit.getScheduler().runTaskLater(plugin, () -> finishSkill(boss), skillDurationTicks);
        return true;
    }

    private void finishSkill(SkeBoss boss) {
        if (!boss.getEntity().isValid()) {
            return;
        }

        LivingEntity entity = boss.getEntity();
        ActiveModel model = boss.getActiveModel();

        // [수정 3] 스킬 애니메이션 종료 후 idle로 복귀
        AnimationHandler handler = model.getAnimationHandler();
        handler.stopAnimation(settings.getSkillAnimation());
        playIdle(model);

        entity.setGravity(true);
        if (entity instanceof Mob mob) {
            mob.setAI(true);
        }

        boss.setCastingSkill(false);
    }

    private void playIdle(ActiveModel model) {
        model.getAnimationHandler().playAnimation(
                settings.getIdleAnimation(),
                settings.getBlendIn(),
                settings.getBlendOut(),
                1.0,
                true
        );
    }

    /**
     * ModelEngine 애니메이션 길이(틱) 추정. 없으면 40틱(2초) 기본값.
     */
    private int estimateSkillDurationTicks(AnimationHandler handler, String animationName) {
        try {
            var animation = handler.getAnimation(animationName);
            if (animation != null) {
                return Math.max(20, (int) Math.ceil(animation.getLength() * 20.0));
            }
        } catch (Exception ignored) {
            // API 차이 시 기본값 사용
        }
        return 40;
    }

    public void remove(SkeBoss boss) {
        LivingEntity entity = boss.getEntity();
        activeBosses.remove(entity.getUniqueId());

        ModeledEntity modeled = boss.getModeledEntity();
        if (modeled != null) {
            modeled.destroy();
        }
        if (entity.isValid()) {
            entity.remove();
        }
    }

    public void removeAll() {
        activeBosses.values().forEach(this::remove);
        activeBosses.clear();
    }

    public SkeBoss getBoss(UUID entityId) {
        return activeBosses.get(entityId);
    }

    public boolean isBoss(LivingEntity entity) {
        return activeBosses.containsKey(entity.getUniqueId());
    }

    private static String colorize(String input) {
        return input.replace('&', '\u00A7');
    }
}
