package com.skeboss.minion;

import com.skeboss.SkeBossPlugin;
import org.bukkit.Bukkit;
import org.bukkit.Location;
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
        MinionConfig config = manager.getConfig();

        for (SkeMinion minion : manager.getMinions()) {
            LivingEntity entity = minion.getEntity();
            if (!entity.isValid() || entity.isDead() || !minion.isReady()) {
                continue;
            }

            if (config.isFireImmune()) {
                entity.setFireTicks(0);
            }

            manager.syncBossBarViewers(minion);
            minion.updateBossBar();

            if (config.isBacklineMode()) {
                tickBackline(minion, config);
            } else {
                tickAggressive(minion, config);
            }
        }
    }

    private void tickAggressive(SkeMinion minion, MinionConfig config) {
        LivingEntity entity = minion.getEntity();
        Player target = minion.findNearestPlayer(config.getFollowRange());
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

        if (distance <= config.getMeleeRange()) {
            manager.meleeAttack(minion, target);
        } else if (distance <= config.getFollowRange()) {
            manager.playWalk(minion);
        }
    }

    private void tickBackline(SkeMinion minion, MinionConfig config) {
        LivingEntity entity = minion.getEntity();
        Location home = minion.getHomeLocation();
        if (home == null || home.getWorld() == null) {
            tickAggressive(minion, config);
            return;
        }

        double distFromHome = entity.getLocation().distance(home);

        if (distFromHome > config.getLeashRadius()) {
            if (entity instanceof Mob mob) {
                mob.setTarget(null);
            }
            manager.returnToHome(minion);
            return;
        }

        Player target = minion.findNearestPlayerNear(home, config.getGuardRadius());
        if (target == null) {
            if (entity instanceof Mob mob) {
                mob.setTarget(null);
            }
            if (distFromHome > config.getHomeTolerance()) {
                manager.returnToHome(minion);
            }
            return;
        }

        double targetDistFromHome = target.getLocation().distance(home);
        if (targetDistFromHome > config.getGuardRadius()) {
            if (entity instanceof Mob mob) {
                mob.setTarget(null);
            }
            if (distFromHome > config.getHomeTolerance()) {
                manager.returnToHome(minion);
            }
            return;
        }

        manager.faceTarget(minion, target);
        double distance = entity.getLocation().distance(target.getLocation());

        if (entity instanceof Mob mob) {
            mob.setAI(true);
            if (wouldExceedLeash(entity.getLocation(), target.getLocation(), home, config.getLeashRadius())) {
                mob.setTarget(null);
                manager.returnToHome(minion);
                return;
            }
            mob.setTarget(target);
        }

        if (distance <= config.getMeleeRange()) {
            manager.meleeAttack(minion, target);
        } else {
            manager.playWalk(minion);
        }
    }

    private static boolean wouldExceedLeash(Location from, Location targetLoc, Location home, double leashRadius) {
        double stepX = targetLoc.getX() - from.getX();
        double stepZ = targetLoc.getZ() - from.getZ();
        double lenSq = stepX * stepX + stepZ * stepZ;
        if (lenSq < 0.0001) {
            return from.distance(home) > leashRadius;
        }
        double len = Math.sqrt(lenSq);
        double nextX = from.getX() + (stepX / len) * Math.min(len, 1.5);
        double nextZ = from.getZ() + (stepZ / len) * Math.min(len, 1.5);
        Location next = new Location(from.getWorld(), nextX, from.getY(), nextZ);
        return next.distance(home) > leashRadius;
    }

    public void start() {
        int interval = manager.getConfig().getAiIntervalTicks();
        Bukkit.getScheduler().runTaskTimer(plugin, this, interval, interval);
    }
}
