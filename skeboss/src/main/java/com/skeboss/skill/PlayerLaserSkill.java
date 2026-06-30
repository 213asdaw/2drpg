package com.skeboss.skill;

import com.skeboss.SkeBossPlugin;
import com.skeboss.boss.BeamSettings;
import com.skeboss.boss.SkillDefinition;
import com.skeboss.weapon.WeaponManager;
import org.bukkit.Location;
import org.bukkit.Sound;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.scheduler.BukkitRunnable;
import org.bukkit.util.Vector;

import java.util.HashSet;
import java.util.Set;

public final class PlayerLaserSkill {

    private PlayerLaserSkill() {
    }

    public static boolean execute(SkeBossPlugin plugin, WeaponManager weapons, Player player, SkillDefinition skill) {
        BeamSettings beam = skill.beam();
        if (beam == null) {
            return false;
        }

        new BukkitRunnable() {
            private int counter = 0;

            @Override
            public void run() {
                if (!player.isValid() || player.isDead()) {
                    cancel();
                    return;
                }

                Vector direction = player.getEyeLocation().getDirection().clone().normalize();
                Location start = player.getEyeLocation();
                BeamUtil.BeamResult result = BeamUtil.traceBeam(start, direction, skill.range(), beam);

                int interval = Math.max(1, beam.damageIntervalTicks());
                if (counter % interval == 0) {
                    if (counter == 0) {
                        player.getWorld().playSound(start, Sound.ENTITY_GUARDIAN_ATTACK, 1.0f, 0.8f);
                    }
                    applyDamage(
                            weapons,
                            player,
                            start,
                            result.direction(),
                            result.length(),
                            beam.width(),
                            counter == 0,
                            skill.knockback()
                    );
                }

                counter++;
                if (counter >= beam.particleTicks()) {
                    cancel();
                }
            }
        }.runTaskTimer(plugin, beam.fireDelayTicks(), 1L);
        return true;
    }

    private static void applyDamage(
            WeaponManager weapons,
            Player shooter,
            Location start,
            Vector direction,
            double length,
            double width,
            boolean knockback,
            double knockbackPower
    ) {
        Vector unit = direction.clone().normalize();
        double damage = weapons.getPlayerBeamDamage(shooter);
        Set<LivingEntity> hit = new HashSet<>();

        for (LivingEntity entity : shooter.getWorld().getLivingEntities()) {
            if (!weapons.canTarget(shooter, entity)) {
                continue;
            }
            if (BeamUtil.isInsideBeam(start, unit, length, width, entity)) {
                hit.add(entity);
            }
        }

        for (LivingEntity entity : hit) {
            entity.damage(damage, shooter);
            if (knockback) {
                Vector kb = unit.clone().multiply(knockbackPower);
                kb.setY(0.25);
                entity.setVelocity(kb);
            }
        }
    }
}
