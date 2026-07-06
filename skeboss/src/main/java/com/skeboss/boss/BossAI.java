package com.skeboss.boss;

import com.skeboss.SkeBossPlugin;
import org.bukkit.Bukkit;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Mob;
import org.bukkit.entity.Player;

public final class BossAI implements Runnable {

    private final SkeBossPlugin plugin;
    private final BossManager manager;

    public BossAI(SkeBossPlugin plugin, BossManager manager) {
        this.plugin = plugin;
        this.manager = manager;
    }

    @Override
    public void run() {
        for (SkeBoss boss : manager.getBosses()) {
            LivingEntity entity = boss.getEntity();
            BossConfig config = boss.getConfig();
            if (!entity.isValid() || entity.isDead() || !boss.isReady()) {
                continue;
            }

            manager.syncBossBarViewers(boss);
            boss.updateBossBar();

            if (config.isFireImmune()) {
                entity.setFireTicks(0);
            }

            Player target = manager.findNearestEnemy(boss, entity, config.getFollowRange());
            boss.setTarget(target);

            if (boss.isCastingSkill()) {
                continue;
            }

            if (target == null) {
                if (entity instanceof Mob mob) {
                    mob.setTarget(null);
                }
                continue;
            }

            manager.faceTarget(boss, target);

            double distance = entity.getLocation().distance(target.getLocation());

            if (entity instanceof Mob mob) {
                mob.setAI(true);
                mob.setTarget(target);
            }

            SkillDefinition readySkill = pickSkill(boss, distance);
            if (readySkill != null && canCastSkill(boss, readySkill)) {
                if (manager.castSkill(boss, readySkill)) {
                    continue;
                }
            }

            if (distance <= config.getMeleeRange()) {
                manager.meleeAttack(boss, target);
            } else if (distance <= config.getFollowRange()) {
                manager.playWalk(boss);
            }
        }
    }

    private boolean canCastSkill(SkeBoss boss, SkillDefinition skill) {
        Player skillTarget = manager.resolveSkillTarget(boss, skill.range());
        if (skillTarget != null) {
            return true;
        }
        if (skill.isUntitledSkill()) {
            String untitled = skill.untitledSkill().toLowerCase();
            return untitled.contains("shield") || untitled.contains("방패");
        }
        if (skill.isBombSkill()) {
            String bomb = skill.bombSkill().toLowerCase();
            return bomb.contains("self") || bomb.contains("explode") || bomb.contains("자폭");
        }
        return false;
    }

    private SkillDefinition pickSkill(SkeBoss boss, double distance) {
        SkillDefinition best = null;
        int bestPriority = Integer.MIN_VALUE;

        for (SkillDefinition skill : boss.getConfig().getSkills()) {
            if (!boss.isSkillReady(skill)) {
                continue;
            }
            if (distance > skill.range() || distance < skill.minRange()) {
                continue;
            }
            if (best == null || skill.priority() > bestPriority) {
                best = skill;
                bestPriority = skill.priority();
            }
        }
        return best;
    }

    public void start() {
        int interval = manager.getConfig().getAiIntervalTicks();
        Bukkit.getScheduler().runTaskTimer(plugin, this, interval, interval);
    }
}
