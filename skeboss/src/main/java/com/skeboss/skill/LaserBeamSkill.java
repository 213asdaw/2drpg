package com.skeboss.skill;

import com.skeboss.SkeBossPlugin;
import com.skeboss.boss.BeamSettings;
import com.skeboss.boss.SkeBoss;
import com.skeboss.boss.SkillDefinition;
import org.bukkit.Color;
import org.bukkit.Location;
import org.bukkit.Particle;
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

    public static void execute(SkeBossPlugin plugin, SkeBoss boss, SkillDefinition skill) {
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

                BeamResult result = drawBeam(entity, skill.range(), beam);
                if (counter == 0) {
                    applyDamage(entity, skill, result.direction(), result.length(), beam.width());
                }

                counter++;
                if (counter >= beam.particleTicks()) {
                    cancel();
                }
            }
        }.runTaskTimer(plugin, beam.fireDelayTicks(), 1L);
    }

    private static BeamResult drawBeam(LivingEntity entity, double maxLength, BeamSettings beam) {
        Location start = beamOrigin(entity);
        Vector direction = start.getDirection().normalize();
        World world = start.getWorld();
        Particle.DustOptions red = new Particle.DustOptions(Color.fromRGB(255, 25, 25), beam.particleSize());

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
        world.spawnParticle(Particle.REDSTONE, point, 4, 0.03, 0.03, 0.03, 0, red);
        world.spawnParticle(Particle.END_ROD, point, 1, 0, 0, 0, 0);
    }

    private static void applyDamage(
            LivingEntity shooter,
            SkillDefinition skill,
            Vector direction,
            double length,
            double width
    ) {
        Location start = beamOrigin(shooter);
        Set<Player> hit = new HashSet<>();

        for (Player player : shooter.getWorld().getPlayers()) {
            if (!player.isValid() || player.isDead() || player.equals(shooter)) {
                continue;
            }
            if (isInsideBeam(start, direction, length, width, player)) {
                hit.add(player);
            }
        }

        for (Player player : hit) {
            player.damage(skill.damage(), shooter);
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

    private static Location beamOrigin(LivingEntity entity) {
        Location eye = entity.getEyeLocation();
        eye.setPitch(0.0f);
        return eye;
    }

    private static boolean isBlocking(Block block) {
        return block.getType().isSolid() && !block.isPassable();
    }

    private record BeamResult(Vector direction, double length) {
    }
}
