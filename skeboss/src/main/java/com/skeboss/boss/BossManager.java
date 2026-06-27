package com.skeboss.boss;

import com.skeboss.SkeBossPlugin;
import com.skeboss.modelengine.ModelEngineBridge;
import com.skeboss.skill.LaserBeamSkill;
import com.skeboss.util.TextUtil;
import org.bukkit.GameMode;
import org.bukkit.Bukkit;
import org.bukkit.Location;
import org.bukkit.attribute.Attribute;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Mob;
import org.bukkit.entity.Player;
import org.bukkit.entity.Zombie;
import org.bukkit.metadata.FixedMetadataValue;
import org.bukkit.scheduler.BukkitTask;
import org.bukkit.util.Vector;

import java.util.Collection;
import java.util.Comparator;
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
                    config.isHideBaseEntity(),
                    config.getModelScale(),
                    config.getHitboxScale()
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
        float yaw = (float) Math.toDegrees(Math.atan2(-dx, dz)) + config.getFaceYawOffset();
        entity.setRotation(yaw, 0.0f);
        if (boss.getModel() != null) {
            modelEngine.syncBodyRotation(boss.getModel(), yaw);
        }
    }

    public boolean isValidTarget(Player player) {
        return player != null
                && player.isValid()
                && !player.isDead()
                && player.getGameMode() != GameMode.SPECTATOR
                && player.getGameMode() != GameMode.CREATIVE;
    }

    public boolean isEnemy(SkeBoss boss, Player player) {
        if (!isValidTarget(player)) {
            return false;
        }
        if ("aggro".equalsIgnoreCase(config.getTargetMode())) {
            return boss.hasAggro(player, config.getAggroDropMs());
        }
        return true;
    }

    public Player findNearestEnemy(SkeBoss boss, LivingEntity entity, double range) {
        return entity.getWorld().getPlayers().stream()
                .filter(player -> isEnemy(boss, player))
                .filter(player -> player.getLocation().distanceSquared(entity.getLocation()) <= range * range)
                .min(Comparator.comparingDouble(player -> player.getLocation().distanceSquared(entity.getLocation())))
                .orElse(null);
    }

    public Player resolveSkillTarget(SkeBoss boss, double range) {
        Player current = boss.getTarget();
        if (current != null && isEnemy(boss, current)
                && current.getWorld().equals(boss.getEntity().getWorld())
                && current.getLocation().distanceSquared(boss.getEntity().getLocation()) <= range * range) {
            return current;
        }
        return findNearestEnemy(boss, boss.getEntity(), range);
    }

    public void startSkillTracking(SkeBoss boss, double trackRange) {
        stopSkillTracking(boss);
        BukkitTask task = Bukkit.getScheduler().runTaskTimer(plugin, () -> {
            if (!boss.getEntity().isValid() || boss.getEntity().isDead() || !boss.isCastingSkill()) {
                stopSkillTracking(boss);
                return;
            }
            Player target = findNearestEnemy(boss, boss.getEntity(), trackRange);
            if (target != null) {
                boss.setTarget(target);
                faceTarget(boss, target);
            }
        }, 0L, 1L);
        boss.setAimTask(task);
    }

    public void stopSkillTracking(SkeBoss boss) {
        boss.cancelAimTask();
    }

    public Location getBeamOrigin(SkeBoss boss) {
        LivingEntity entity = boss.getEntity();
        Location loc = entity.getLocation().clone();
        loc.add(0, entity.getHeight() * 0.75, 0);
        loc.setPitch(0.0f);
        return loc;
    }

    public Vector getBeamDirection(SkeBoss boss, double range) {
        Player target = resolveSkillTarget(boss, range);
        Location origin = getBeamOrigin(boss);

        if (target != null) {
            boss.setTarget(target);
            Vector toTarget = target.getLocation().add(0, target.getHeight() * 0.5, 0).toVector()
                    .subtract(origin.toVector());
            toTarget.setY(0);
            if (toTarget.lengthSquared() > 0.0001) {
                return toTarget.normalize();
            }
        }

        float yaw = entityYaw(boss.getEntity());
        return new Vector(-Math.sin(Math.toRadians(yaw)), 0, Math.cos(Math.toRadians(yaw)));
    }

    private float entityYaw(LivingEntity entity) {
        return entity.getLocation().getYaw();
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

        double trackRange = Math.max(skill.range(), config.getFollowRange());
        Player target = resolveSkillTarget(boss, trackRange);
        if (target != null) {
            boss.setTarget(target);
            faceTarget(boss, target);
        }

        startSkillTracking(boss, trackRange);

        modelEngine.playLoopAnimation(
                boss.getModel(),
                skill.animation(),
                config.getBlendIn(),
                config.getBlendOut()
        );
        boss.setCurrentAnimation(skill.animation());

        int duration = modelEngine.estimateDurationTicks(boss.getModel(), skill.animation(), skill.durationTicks());

        if (skill.isBeamSkill()) {
            LaserBeamSkill.execute(plugin, this, boss, skill);
        } else {
            Bukkit.getScheduler().runTaskLater(plugin, () -> applySkillDamage(boss, skill), Math.max(5, duration / 2));
        }
        Bukkit.getScheduler().runTaskLater(plugin, () -> finishSkill(boss, skill), duration);
        return true;
    }

    private void applySkillDamage(SkeBoss boss, SkillDefinition skill) {
        LivingEntity entity = boss.getEntity();
        if (!entity.isValid()) {
            return;
        }

        Location origin = entity.getLocation();
        Collection<EntityTarget> targets = findTargets(boss, origin, skill.range(), skill.aoeRadius());
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
        if (boss.isCastingSkill() || !isEnemy(boss, target)) {
            return;
        }
        target.damage(config.getMeleeDamage(), boss.getEntity());
    }

    private void finishSkill(SkeBoss boss, SkillDefinition skill) {
        if (!boss.getEntity().isValid()) {
            return;
        }

        stopSkillTracking(boss);

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

    private Collection<EntityTarget> findTargets(SkeBoss boss, Location origin, double range, double aoeRadius) {
        Map<UUID, EntityTarget> found = new ConcurrentHashMap<>();
        double checkRadius = aoeRadius > 0 ? aoeRadius : range;

        for (Player player : origin.getWorld().getPlayers()) {
            if (!isEnemy(boss, player)) {
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
