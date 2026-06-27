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
        });

        ModelEngineBridge.BossModel bossModel = modelEngine.attachModel(zombie, config.getModelId());
        modelEngine.playLoopAnimation(bossModel, config.getIdleAnimation(), config.getBlendIn(), config.getBlendOut());

        SkeBoss boss = new SkeBoss(zombie, bossModel, config);
        for (Player online : Bukkit.getOnlinePlayers()) {
            if (online.getWorld().equals(zombie.getWorld())
                    && online.getLocation().distanceSquared(zombie.getLocation()) <= 64 * 64) {
                boss.addViewer(online);
            }
        }

        bosses.put(zombie.getUniqueId(), boss);
        return boss;
    }

    public void playIdle(SkeBoss boss) {
        modelEngine.playLoopAnimation(
                boss.getModel(),
                config.getIdleAnimation(),
                config.getBlendIn(),
                config.getBlendOut()
        );
    }

    public void playWalk(SkeBoss boss) {
        modelEngine.playLoopAnimation(
                boss.getModel(),
                config.getWalkAnimation(),
                config.getBlendIn(),
                config.getBlendOut()
        );
    }

    public boolean castSkill(SkeBoss boss, SkillDefinition skill) {
        if (boss.isCastingSkill() || !boss.isSkillReady(skill)) {
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
        playIdle(boss);

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
        modelEngine.destroy(boss.getModel());
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
