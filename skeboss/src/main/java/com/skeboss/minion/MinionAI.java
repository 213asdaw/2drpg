package com.skeboss.minion;

import com.skeboss.SkeBossPlugin;
import org.bukkit.Bukkit;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Mob;
import org.bukkit.entity.Player;

public final class MinionAI implements Runnable {

    private final SkeBossPlugin plugin;
    private final MinionManager manager;

    public MinionAI(SkeBossPlugin plugin, MinionManager manager) {
        this.plugin = plugin;
        this.manager = manager;
    }

    @Override
    public void run() {
        for (SkeMinion minion : manager.getMinions()) {
            LivingEntity entity = minion.getEntity();
            MinionPreset preset = minion.getPreset();
            if (!entity.isValid() || entity.isDead() || !minion.isReady()) {
                continue;
            }

            if (preset.isFireImmune()) {
                entity.setFireTicks(0);
            }

            manager.syncBossBarViewers(minion);
            minion.updateBossBar();

            if (preset.isBacklineMode()) {
                tickPassiveUntilHit(minion, preset);
            } else {
                tickAggressive(minion, preset);
            }
        }
    }

    private void tickAggressive(SkeMinion minion, MinionPreset preset) {
        tickCombat(minion, preset, minion.findNearestPlayer(preset.getFollowRange()));
    }

    private void tickPassiveUntilHit(SkeMinion minion, MinionPreset preset) {
        LivingEntity entity = minion.getEntity();

        if (!minion.isProvoked()) {
            if (entity instanceof Mob mob) {
                mob.setAware(false);
                mob.setTarget(null);
            }
            return;
        }

        if (entity instanceof Mob mob) {
            mob.setAware(true);
        }

        Player target = resolveProvokedTarget(minion, preset);
        tickCombat(minion, preset, target);
    }

    private Player resolveProvokedTarget(SkeMinion minion, MinionPreset preset) {
        Player attacker = minion.getProvokeTarget();
        if (attacker != null) {
            double range = preset.getFollowRange();
            if (attacker.getWorld().equals(minion.getEntity().getWorld())
                    && minion.getEntity().getLocation().distanceSquared(attacker.getLocation()) <= range * range) {
                return attacker;
            }
        }
        return minion.findNearestPlayer(preset.getFollowRange());
    }

    private void tickCombat(SkeMinion minion, MinionPreset preset, Player target) {
        LivingEntity entity = minion.getEntity();
        if (target == null) {
            if (entity instanceof Mob mob) {
                mob.setTarget(null);
            }
            return;
        }

        manager.faceTarget(minion, target);

        double distance = entity.getLocation().distance(target.getLocation());

        if (entity instanceof Mob mob) {
            mob.setAI(true);
            mob.setTarget(target);
        }

        if (distance <= preset.getMeleeRange()) {
            manager.meleeAttack(minion, target);
        } else if (distance <= preset.getFollowRange()) {
            manager.playWalk(minion);
        }
    }

    public void start() {
        int interval = manager.getConfig().getAiIntervalTicks();
        Bukkit.getScheduler().runTaskTimer(plugin, this, interval, interval);
    }
}
