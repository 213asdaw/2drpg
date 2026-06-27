package com.skeboss.skill;

import com.skeboss.boss.BeamSettings;
import org.bukkit.Color;
import org.bukkit.Location;
import org.bukkit.Particle;
import org.bukkit.World;
import org.bukkit.block.Block;
import org.bukkit.entity.LivingEntity;
import org.bukkit.util.Vector;

public final class BeamUtil {

    private BeamUtil() {
    }

    public static BeamResult traceBeam(Location start, Vector direction, double maxLength, BeamSettings beam) {
        World world = start.getWorld();
        Particle.DustOptions red = new Particle.DustOptions(Color.fromRGB(255, 20, 20), beam.particleSize());
        Vector unit = direction.clone().normalize();

        double traveled = maxLength;
        for (double d = 0; d <= maxLength; d += beam.particleStep()) {
            Location point = start.clone().add(unit.clone().multiply(d));
            if (d > 0.4 && isBlocking(point.getBlock())) {
                traveled = d;
                spawnBeamPoint(world, point, red);
                break;
            }
            traveled = d;
            spawnBeamPoint(world, point, red);
        }
        return new BeamResult(unit, traveled);
    }

    public static void spawnBeamPoint(World world, Location point, Particle.DustOptions red) {
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

    public static boolean isInsideBeam(Location start, Vector direction, double length, double width, LivingEntity entity) {
        double widthSq = width * width;
        Location base = entity.getLocation();
        double height = entity.getHeight();
        for (double ratio = 0.2; ratio <= 1.0; ratio += 0.2) {
            Location sample = base.clone().add(0, height * ratio, 0);
            if (isInsideBeamAt(start, direction, length, widthSq, sample)) {
                return true;
            }
        }
        return false;
    }

    private static boolean isInsideBeamAt(
            Location start,
            Vector direction,
            double length,
            double widthSq,
            Location target
    ) {
        Vector startVec = start.toVector();
        Vector toTarget = target.toVector().subtract(startVec);
        double projection = toTarget.dot(direction);
        if (projection < 0 || projection > length) {
            return false;
        }
        Vector closest = startVec.clone().add(direction.clone().multiply(projection));
        return target.toVector().distanceSquared(closest) <= widthSq;
    }

    private static boolean isBlocking(Block block) {
        return block.getType().isSolid() && !block.isPassable();
    }

    public record BeamResult(Vector direction, double length) {
    }
}
