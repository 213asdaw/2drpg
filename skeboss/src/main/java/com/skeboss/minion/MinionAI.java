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

            Player target = minion.findNearestPlayer(config.getFollowRange());
            if (target == null) {
                if (entity instanceof Mob mob) {
                    mob.setTarget(null);
                }
                continue;
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
    }

    public void start() {
        int interval = manager.getConfig().getAiIntervalTicks();
        Bukkit.getScheduler().runTaskTimer(plugin, this, interval, interval);
    }
}
