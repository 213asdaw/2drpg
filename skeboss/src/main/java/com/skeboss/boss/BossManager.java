package com.skeboss.boss;

import com.skeboss.SkeBossPlugin;
import com.skeboss.modelengine.ModelEngineBridge;
import com.skeboss.util.TextUtil;
import org.bukkit.Bukkit;
import org.bukkit.Location;
import org.bukkit.attribute.Attribute;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Mob;
import org.bukkit.entity.Player;
import org.bukkit.entity.Zombie;
import org.bukkit.metadata.FixedMetadataValue;
import org.bukkit.util.Vector;

import java.util.Collection;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.logging.Level;

public final class BossManager {

    public static final String METADATA_KEY = "skeboss";

    private final SkeBossPlugin plugin;
    private final ModelEngineBridge modelEngine;
    private final BossConfig config;
    private final Map<UUID, SkeBoss> bosses = new ConcurrentHashMap<>();

    public BossManager(SkeBossPlugin plugin, ModelEngineBridge modelEngine) {
        this.plugin = plugin;
        this.modelEngine = modelEngine;
        this.config = new BossConfig(plugin);
    }

    public SkeBoss spawn(Location location) {
        Location spawnLoc = location.clone();
        spawnLoc.setYaw(spawnLoc.getYaw() + config.getYawOffset());

        Zombie zombie = location.getWorld().spawn(spawnLoc, Zombie.class, entity -> {
            entity.setBaby(false);
            entity.setSilent(true);
            entity.setCanPickupItems(false);
            entity.setRemoveWhenFarAway(false);
            entity.setShouldBurnInDay(false);
            entity.setCustomNameVisible(true);
            entity.setCustomName(TextUtil.color(config.getDisplayName()));
            entity.setMetadata(METADATA_KEY, new FixedMetadataValue(plugin, true));

            Attribute maxHealth = Attribute.GENERIC_MAX_HEALTH;
            if (entity.getAttribute(maxHealth) != null) {
                entity.getAttribute(maxHealth).setBaseValue(config.getMaxHealth());
            }
            entity.setHealth(config.getMaxHealth());

            Attribute follow = Attribute.GENERIC_FOLLOW_RANGE;
            if (entity.getAttribute(follow) != null) {
                entity.getAttribute(follow).setBaseValue(config.getFollowRange());
            }

            Attribute speed = Attribute.GENERIC_MOVEMENT_SPEED;
            if (entity.getAttribute(speed) != null) {
                entity.getAttribute(speed).setBaseValue(config.getMovementSpeed());
            }
        });

        SkeBoss boss = new SkeBoss(zombie, null, config);
        bosses.put(zombie.getUniqueId(), boss);

        int delay = Math.max(1, config.getSpawnDelayTicks());
        Bukkit.getScheduler().runTaskLater(plugin, () -> finishSpawn(boss), delay);
        return boss;
    }

    private void finishSpawn(SkeBoss boss) {
        LivingEntity entity = boss.getEntity();
        if (!entity.isValid() || entity.isDead()) {
            bosses.remove(entity.getUniqueId());
            return;
        }

        try {
            ModelEngineBridge.BossModel bossModel = modelEngine.attachModel(
                    entity,
                    config.getModelId(),
                    config.isHideBaseEntity()
            );
            boss.setModel(bossModel);

            playAnimation(boss, config.getWalkAnimation());

            modelEngine.syncNearbyPlayers(bossModel, entity, config.getViewerSyncRadius());
            registerBossBarViewers(boss);

            plugin.getLogger().info("보스 스폰 완료: " + entity.getUniqueId() + " (모델: " + config.getModelId() + ")");
        } catch (RuntimeException ex) {
            plugin.getLogger().log(Level.SEVERE, "보스 모델 적용 실패 — 좀비만 남습니다. /meg reload 확인", ex);
            if (!config.isHideBaseEntity()) {
                entity.setCustomName(TextUtil.color(config.getDisplayName() + " &7(모델 로드 실패)"));
            }
        }
    }

    private void registerBossBarViewers(SkeBoss boss) {
        for (Player online : Bukkit.getOnlinePlayers()) {
            if (online.getWorld().equals(boss.getEntity().getWorld())
                    && online.getLocation().distanceSquared(boss.getEntity().getLocation())
                    <= config.getViewerSyncRadius() * config.getViewerSyncRadius()) {
                boss.addViewer(online);
            }
        }
    }

    public void syncBossViewers(SkeBoss boss, Player player) {
        if (boss.getModel() != null) {
            modelEngine.syncNearbyPlayers(boss.getModel(), boss.getEntity(), config.getViewerSyncRadius());
        }
        boss.addViewer(player);
    }

    public void playIdle(SkeBoss boss) {
        playAnimation(boss, config.getIdleAnimation());
    }

    public void playWalk(SkeBoss boss) {
        playAnimation(boss, config.getWalkAnimation());
    }

    public void playAnimation(SkeBoss boss, String animation) {
        if (!boss.isReady() || animation == null || animation.isBlank() || "none".equalsIgnoreCase(animation)) {
            return;
        }
        if (animation.equals(boss.getCurrentAnimation())) {
            return;
        }
        modelEngine.playLoopAnimation(
                boss.getModel(),
                animation,
                config.getBlendIn(),
                config.getBlendOut()
        );
        boss.setCurrentAnimation(animation);
    }

    public void clearAnimationState(SkeBoss boss) {
        boss.setCurrentAnimation(null);
    }

    public void faceTarget(SkeBoss boss, Player target) {
        if (target == null || !boss.getEntity().isValid()) {
            return;
        }
        LivingEntity entity = boss.getEntity();
        double dx = target.getX() - entity.getX();
        double dz = target.getZ() - entity.getZ();
        if (dx * dx + dz * dz < 0.0001) {
            return;
        }
        float yaw = (float) Math.toDegrees(Math.atan2(-dx, dz));
        entity.setRotation(yaw, 0.0f);
    }

    public boolean castSkill(SkeBoss boss, SkillDefinition skill) {
        if (boss.isCastingSkill() || !boss.isReady() || !boss.isSkillReady(skill)) {
            return false;
        }

        LivingEntity entity = boss.getEntity();
        boss.setCastingSkill(true);
        boss.setCurrentSkillId(skill.id());
        boss.setSkillCooldown(skill);

        if (entity instanceof Mob mob) {
            mob.setAI(false);
        }
        entity.setVelocity(new Vector(0, 0, 0));

        modelEngine.playLoopAnimation(
                boss.getModel(),
                skill.animation(),
                config.getBlendIn(),
                config.getBlendOut()
        );
        boss.setCurrentAnimation(skill.animation());

        int duration = modelEngine.estimateDurationTicks(boss.getModel(), skill.animation(), skill.durationTicks());

        Bukkit.getScheduler().runTaskLater(plugin, () -> applySkillDamage(boss, skill), Math.max(5, duration / 2));
        Bukkit.getScheduler().runTaskLater(plugin, () -> finishSkill(boss, skill), duration);
        return true;
    }

    private void applySkillDamage(SkeBoss boss, SkillDefinition skill) {
        LivingEntity entity = boss.getEntity();
        if (!entity.isValid()) {
            return;
        }

        Location origin = entity.getLocation();
        Collection<EntityTarget> targets = findTargets(origin, skill.range(), skill.aoeRadius());
        for (EntityTarget target : targets) {
            target.player().damage(skill.damage(), entity);
            Vector knockback = target.player().getLocation().toVector()
                    .subtract(origin.toVector())
                    .normalize()
                    .multiply(skill.knockback());
            knockback.setY(0.35);
            target.player().setVelocity(knockback);
        }
    }

    public void meleeAttack(SkeBoss boss, Player target) {
        if (boss.isCastingSkill()) {
            return;
        }
        target.damage(config.getMeleeDamage(), boss.getEntity());
    }

    private void finishSkill(SkeBoss boss, SkillDefinition skill) {
        if (!boss.getEntity().isValid()) {
            return;
        }

        modelEngine.stopAnimation(boss.getModel(), skill.animation());
        clearAnimationState(boss);
        playWalk(boss);

        LivingEntity entity = boss.getEntity();
        if (entity instanceof Mob mob) {
            mob.setAI(true);
        }

        boss.setCastingSkill(false);
        boss.setCurrentSkillId(null);
    }

    private Collection<EntityTarget> findTargets(Location origin, double range, double aoeRadius) {
        Map<UUID, EntityTarget> found = new ConcurrentHashMap<>();
        double checkRadius = aoeRadius > 0 ? aoeRadius : range;

        for (Player player : origin.getWorld().getPlayers()) {
            if (!player.isValid() || player.isDead()) {
                continue;
            }
            double distance = player.getLocation().distance(origin);
            if (distance > checkRadius) {
                continue;
            }
            if (aoeRadius <= 0 && distance > range) {
                continue;
            }
            found.put(player.getUniqueId(), new EntityTarget(player, distance));
        }
        return found.values();
    }

    public void remove(SkeBoss boss) {
        bosses.remove(boss.getId());
        boss.removeAllViewers();
        if (boss.getModel() != null) {
            modelEngine.destroy(boss.getModel());
        }
        if (boss.getEntity().isValid()) {
            boss.getEntity().remove();
        }
    }

    public void removeAll() {
        bosses.values().forEach(this::remove);
        bosses.clear();
    }

    public SkeBoss getBoss(UUID id) {
        return bosses.get(id);
    }

    public Collection<SkeBoss> getBosses() {
        return bosses.values();
    }

    public boolean isBoss(LivingEntity entity) {
        return bosses.containsKey(entity.getUniqueId());
    }

    public BossConfig getConfig() {
        return config;
    }

    private record EntityTarget(Player player, double distance) {
    }
}
