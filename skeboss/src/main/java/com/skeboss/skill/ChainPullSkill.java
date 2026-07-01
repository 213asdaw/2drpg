package com.skeboss.skill;

import com.skeboss.SkeBossPlugin;
import com.skeboss.boss.BossManager;
import com.skeboss.boss.ChainSettings;
import com.skeboss.boss.SkeBoss;
import com.skeboss.boss.SkillDefinition;
import org.bukkit.Location;
import org.bukkit.Material;
import org.bukkit.Particle;
import org.bukkit.Sound;
import org.bukkit.World;
import org.bukkit.block.Block;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.scheduler.BukkitRunnable;
import org.bukkit.util.Vector;

public final class ChainPullSkill {

    private ChainPullSkill() {
    }

    public static void execute(SkeBossPlugin plugin, BossManager manager, SkeBoss boss, SkillDefinition skill) {
        ChainSettings chain = skill.chain();
        if (chain == null) {
            return;
        }

        LivingEntity entity = boss.getEntity();

        new BukkitRunnable() {
            private boolean pulling;
            private Player latched;
            private Vector direction;
            private Location hook;
            private double traveled;
            private int pullTicks;

            @Override
            public void run() {
                if (!entity.isValid() || entity.isDead() || !boss.isCastingSkill()) {
                    cancel();
                    return;
                }

                Location origin = manager.getBeamOrigin(boss);

                if (!pulling) {
                    extendChain(origin, chain, skill);
                    return;
                }

                pullTarget(origin, chain);
            }

            private void extendChain(Location origin, ChainSettings chain, SkillDefinition skill) {
                Player target = manager.resolveSkillTarget(boss, skill.range());
                if (target != null) {
                    boss.setTarget(target);
                    manager.faceTarget(boss, target, true);
                }

                if (direction == null) {
                    traveled = 0;
                    entity.getWorld().playSound(origin, Sound.ENTITY_FISHING_BOBBER_THROW, 1.1f, 0.55f);
                }

                direction = manager.getBeamDirection(boss, skill.range()).clone();
                traveled = Math.min(skill.range(), traveled + chain.chainSpeed());
                hook = origin.clone().add(direction.clone().multiply(traveled));

                drawChain(origin.getWorld(), origin, hook);

                if (isBlocking(hook.getBlock())) {
                    entity.getWorld().playSound(hook, Sound.BLOCK_CHAIN_BREAK, 0.8f, 1.2f);
                    cancel();
                    return;
                }

                Player hit = findLatchedPlayer(manager, boss, hook, chain.hitRadius());
                if (hit != null) {
                    latched = hit;
                    pulling = true;
                    pullTicks = 0;
                    entity.getWorld().playSound(hit.getLocation(), Sound.BLOCK_CHAIN_PLACE, 1.0f, 0.7f);
                    if (skill.damage() > 0) {
                        hit.damage(skill.damage(), entity);
                    }
                    return;
                }

                if (traveled >= skill.range()) {
                    entity.getWorld().playSound(hook, Sound.BLOCK_CHAIN_BREAK, 0.6f, 1.4f);
                    cancel();
                }
            }

            private void pullTarget(Location origin, ChainSettings chain) {
                if (latched == null || !latched.isValid() || latched.isDead()) {
                    cancel();
                    return;
                }

                Location anchor = latched.getLocation().clone().add(0, latched.getHeight() * 0.45, 0);
                drawChain(origin.getWorld(), origin, anchor);

                Vector toBoss = origin.toVector()
                        .add(new Vector(0, 0.5, 0))
                        .subtract(latched.getLocation().add(0, 1.0, 0).toVector());
                double distance = toBoss.length();

                if (distance > 0.35) {
                    Vector pull = toBoss.normalize().multiply(chain.pullSpeed());
                    latched.setVelocity(pull);
                } else {
                    latched.setVelocity(new Vector(0, 0, 0));
                }

                pullTicks++;
                if (distance <= 2.2 || pullTicks >= chain.pullTicks()) {
                    latched.setVelocity(new Vector(0, 0, 0));
                    entity.getWorld().playSound(latched.getLocation(), Sound.ENTITY_IRON_GOLEM_ATTACK, 0.9f, 1.1f);
                    cancel();
                }
            }

            private Player findLatchedPlayer(BossManager manager, SkeBoss boss, Location point, double radius) {
                double radiusSq = radius * radius;
                Player closest = null;
                double best = radiusSq;

                for (Player player : point.getWorld().getPlayers()) {
                    if (!manager.isEnemy(boss, player)) {
                        continue;
                    }
                    Location center = player.getLocation().add(0, player.getHeight() * 0.5, 0);
                    double distSq = center.distanceSquared(point);
                    if (distSq <= best) {
                        best = distSq;
                        closest = player;
                    }
                }
                return closest;
            }
        }.runTaskTimer(plugin, chain.fireDelayTicks(), 1L);
    }

    private static void drawChain(World world, Location from, Location to) {
        Vector delta = to.toVector().subtract(from.toVector());
        double length = delta.length();
        if (length < 0.01) {
            return;
        }
        Vector unit = delta.clone().normalize();

        for (double d = 0; d <= length; d += 0.35) {
            Location point = from.clone().add(unit.clone().multiply(d));
            world.spawnParticle(Particle.CRIT, point, 2, 0.02, 0.02, 0.02, 0, null, true);
            world.spawnParticle(Particle.SMOKE_NORMAL, point, 1, 0.01, 0.01, 0.01, 0, null, true);
            world.spawnParticle(
                    Particle.BLOCK_CRACK,
                    point,
                    3,
                    0.05,
                    0.05,
                    0.05,
                    0,
                    Material.CHAIN.createBlockData(),
                    true
            );
        }

        world.spawnParticle(Particle.END_ROD, to, 4, 0.05, 0.05, 0.05, 0.01, null, true);
    }

    private static boolean isBlocking(Block block) {
        return block.getType().isSolid() && !block.isPassable();
    }
}
