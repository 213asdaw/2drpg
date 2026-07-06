package com.skeboss.skill;

import com.skeboss.SkeBossPlugin;
import com.skeboss.boss.ChainSettings;
import com.skeboss.boss.SkillDefinition;
import com.skeboss.weapon.WeaponManager;
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

public final class PlayerChainSkill {

    private PlayerChainSkill() {
    }

    public static boolean execute(SkeBossPlugin plugin, WeaponManager weapons, Player player, SkillDefinition skill) {
        ChainSettings chain = skill.chain();
        if (chain == null) {
            return false;
        }

        new BukkitRunnable() {
            private boolean pulling;
            private LivingEntity latched;
            private double traveled;
            private int pullTicks;

            @Override
            public void run() {
                if (!player.isValid() || player.isDead()) {
                    cancel();
                    return;
                }

                Location origin = player.getEyeLocation();

                if (!pulling) {
                    extendChain(origin, chain, skill, weapons);
                    return;
                }
                pullTarget(origin, chain);
            }

            private void extendChain(Location origin, ChainSettings chain, SkillDefinition skill, WeaponManager weapons) {
                if (traveled == 0) {
                    player.getWorld().playSound(origin, Sound.ENTITY_FISHING_BOBBER_THROW, 1.0f, 0.7f);
                }

                Vector direction = player.getEyeLocation().getDirection().clone().normalize();
                traveled = Math.min(skill.range(), traveled + chain.chainSpeed());
                Location hook = origin.clone().add(direction.clone().multiply(traveled));

                drawChain(origin.getWorld(), origin, hook);

                if (isBlocking(hook.getBlock())) {
                    player.getWorld().playSound(hook, Sound.BLOCK_CHAIN_BREAK, 0.8f, 1.2f);
                    cancel();
                    return;
                }

                LivingEntity hit = findLatched(weapons, player, hook, chain.hitRadius());
                if (hit != null) {
                    latched = hit;
                    pulling = true;
                    pullTicks = 0;
                    player.getWorld().playSound(hit.getLocation(), Sound.BLOCK_CHAIN_PLACE, 1.0f, 0.7f);
                    double damage = weapons.getPlayerSkillDamage(player, skill);
                    if (damage > 0) {
                        hit.damage(damage, player);
                    }
                    return;
                }

                if (traveled >= skill.range()) {
                    player.getWorld().playSound(hook, Sound.BLOCK_CHAIN_BREAK, 0.6f, 1.4f);
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

                Vector toPlayer = origin.toVector()
                        .subtract(latched.getLocation().add(0, latched.getHeight() * 0.5, 0).toVector());
                double distance = toPlayer.length();

                if (distance > 0.35) {
                    latched.setVelocity(toPlayer.normalize().multiply(chain.pullSpeed()));
                } else {
                    latched.setVelocity(new Vector(0, 0, 0));
                }

                pullTicks++;
                if (distance <= 2.5 || pullTicks >= chain.pullTicks()) {
                    latched.setVelocity(new Vector(0, 0, 0));
                    player.getWorld().playSound(latched.getLocation(), Sound.ENTITY_IRON_GOLEM_ATTACK, 0.8f, 1.1f);
                    cancel();
                }
            }
        }.runTaskTimer(plugin, chain.fireDelayTicks(), 1L);
        return true;
    }

    private static LivingEntity findLatched(WeaponManager weapons, Player player, Location point, double radius) {
        double radiusSq = radius * radius;
        LivingEntity closest = null;
        double best = radiusSq;

        for (LivingEntity entity : point.getWorld().getLivingEntities()) {
            if (!weapons.canTarget(player, entity)) {
                continue;
            }
            Location center = entity.getLocation().add(0, entity.getHeight() * 0.5, 0);
            double distSq = center.distanceSquared(point);
            if (distSq <= best) {
                best = distSq;
                closest = entity;
            }
        }
        return closest;
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
