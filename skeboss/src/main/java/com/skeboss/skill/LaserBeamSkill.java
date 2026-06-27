package com.skeboss.skill;

import com.skeboss.SkeBossPlugin;
import com.skeboss.boss.BeamSettings;
import com.skeboss.boss.BossManager;
import com.skeboss.boss.SkeBoss;
import com.skeboss.boss.SkillDefinition;
import org.bukkit.Color;
import org.bukkit.Location;
import org.bukkit.Particle;
import org.bukkit.Sound;
import org.bukkit.World;
import org.bukkit.block.Block;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.scheduler.BukkitRunnable;
import org.bukkit.util.Vector;

import java.util.HashSet;
import java.util.Set;

public final class LaserBeamSkill {

    private LaserBeamSkill() {
    }

    public static void execute(SkeBossPlugin plugin, BossManager manager, SkeBoss boss, SkillDefinition skill) {
        BeamSettings beam = skill.beam();
        if (beam == null) {
            return;
        }

        LivingEntity entity = boss.getEntity();

        new BukkitRunnable() {
            private int counter = 0;

            @Override
            public void run() {
                if (!entity.isValid() || entity.isDead()) {
                    cancel();
                    return;
                }

                Player target = manager.resolveSkillTarget(boss, skill.range());
                if (target != null) {
                    boss.setTarget(target);
                    manager.faceTarget(boss, target);
                }

                Vector direction = manager.getBeamDirection(boss, skill.range());
                Location start = manager.getBeamOrigin(boss);
                BeamResult result = drawBeam(start, direction, skill.range(), beam);

                if (counter == 0) {
                    entity.getWorld().playSound(start, Sound.ENTITY_GUARDIAN_ATTACK, 1.2f, 0.6f);
                    applyDamage(manager, boss, entity, skill, start, result.direction(), result.length(), beam.width());
                }

                counter++;
                if (counter >= beam.particleTicks()) {
                    cancel();
                }
            }
        }.runTaskTimer(plugin, beam.fireDelayTicks(), 1L);
    }

    private static BeamResult drawBeam(Location start, Vector direction, double maxLength, BeamSettings beam) {
        World world = start.getWorld();
        Particle.DustOptions red = new Particle.DustOptions(Color.fromRGB(255, 20, 20), beam.particleSize());

        double traveled = maxLength;
        for (double d = 0; d <= maxLength; d += beam.particleStep()) {
            Location point = start.clone().add(direction.clone().multiply(d));

            if (d > 0.4 && isBlocking(point.getBlock())) {
                traveled = d;
                spawnBeamPoint(world, point, red);
                break;
            }

            traveled = d;
            spawnBeamPoint(world, point, red);
        }

        return new BeamResult(direction, traveled);
    }

    private static void spawnBeamPoint(World world, Location point, Particle.DustOptions red) {
        spawnParticleForced(world, Particle.REDSTONE, point, 10, 0.06, 0.06, 0.06, red);
        spawnParticleForced(world, Particle.CRIT, point, 2, 0.02, 0.02, 0.02, null);
        spawnParticleForced(world, Particle.FLAME, point, 1, 0, 0, 0, null);
        spawnParticleForced(world, Particle.CRIMSON_SPORE, point, 2, 0.04, 0.04, 0.04, null);
    }

    private static void spawnParticleForced(
            World world,
            Particle particle,
            Location point,
            int count,
            double ox,
            double oy,
            double oz,
            Object data
    ) {
        world.spawnParticle(particle, point, count, ox, oy, oz, 0, data, true);
    }

    private static void applyDamage(
            BossManager manager,
            SkeBoss boss,
            LivingEntity shooter,
            SkillDefinition skill,
            Location start,
            Vector direction,
            double length,
            double width
    ) {
        Set<Player> hit = new HashSet<>();

        for (Player player : shooter.getWorld().getPlayers()) {
            if (!manager.isEnemy(boss, player) || player.equals(shooter)) {
                continue;
            }
            if (isInsideBeam(start, direction, length, width, player)) {
                hit.add(player);
            }
        }

        for (Player player : hit) {
            player.damage(manager.getBeamDamage(boss), shooter);
            Vector knockback = direction.clone().multiply(skill.knockback());
            knockback.setY(0.25);
            player.setVelocity(knockback);
        }
    }

    static boolean isInsideBeam(Location start, Vector direction, double length, double width, Player player) {
        Location target = player.getLocation().add(0, player.getHeight() * 0.5, 0);
        Vector startVec = start.toVector();
        Vector toTarget = target.toVector().subtract(startVec);

        double projection = toTarget.dot(direction);
        if (projection < 0 || projection > length) {
            return false;
        }

        Vector closest = startVec.clone().add(direction.clone().multiply(projection));
        return target.toVector().distanceSquared(closest) <= width * width;
    }

    private static boolean isBlocking(Block block) {
        return block.getType().isSolid() && !block.isPassable();
    }

    private record BeamResult(Vector direction, double length) {
    }
}
