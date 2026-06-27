package com.skeboss.boss;

import com.skeboss.SkeBossPlugin;
import org.bukkit.Bukkit;
import org.bukkit.GameMode;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Mob;
import org.bukkit.entity.Player;

import java.util.Comparator;

public final class BossAI implements Runnable {

    private final SkeBossPlugin plugin;
    private final BossManager manager;

    public BossAI(SkeBossPlugin plugin, BossManager manager) {
        this.plugin = plugin;
        this.manager = manager;
    }

    @Override
    public void run() {
        BossConfig config = manager.getConfig();

        for (SkeBoss boss : manager.getBosses()) {
            LivingEntity entity = boss.getEntity();
            if (!entity.isValid() || entity.isDead() || !boss.isReady()) {
                continue;
            }

            boss.updateBossBar();

            if (boss.isCastingSkill()) {
                continue;
            }

            Player target = findNearestPlayer(entity, config.getFollowRange());
            boss.setTarget(target);
            if (target == null) {
                continue;
            }

            manager.faceTarget(boss, target);

            double distance = entity.getLocation().distance(target.getLocation());

            if (entity instanceof Mob mob) {
                mob.setAI(true);
                mob.setTarget(target);
            }

            SkillDefinition readySkill = pickSkill(boss, distance);
            if (readySkill != null && manager.castSkill(boss, readySkill)) {
                continue;
            }

            if (distance <= config.getMeleeRange()) {
                manager.meleeAttack(boss, target);
            } else if (distance <= config.getFollowRange()) {
                manager.playWalk(boss);
            }
        }
    }

    private SkillDefinition pickSkill(SkeBoss boss, double distance) {
        SkillDefinition best = null;
        for (SkillDefinition skill : manager.getConfig().getSkills()) {
            if (!boss.isSkillReady(skill)) {
                continue;
            }
            if (distance > skill.range()) {
                continue;
            }
            if (best == null || skill.damage() > best.damage()) {
                best = skill;
            }
        }
        return best;
    }

    private Player findNearestPlayer(LivingEntity entity, double range) {
        return entity.getWorld().getPlayers().stream()
                .filter(player -> player.isValid() && !player.isDead() && player.getGameMode() != GameMode.SPECTATOR)
                .filter(player -> player.getLocation().distanceSquared(entity.getLocation()) <= range * range)
                .min(Comparator.comparingDouble(player -> player.getLocation().distanceSquared(entity.getLocation())))
                .orElse(null);
    }

    public void start() {
        int interval = manager.getConfig().getAiIntervalTicks();
        Bukkit.getScheduler().runTaskTimer(plugin, this, interval, interval);
    }
}
