package com.skeboss.skill;

import com.skeboss.SkeBossPlugin;
import com.skeboss.boss.SkeBoss;
import org.bukkit.GameMode;
import org.bukkit.Location;
import org.bukkit.Material;
import org.bukkit.Particle;
import org.bukkit.Sound;
import org.bukkit.entity.ArmorStand;
import org.bukkit.entity.Entity;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.meta.ItemMeta;
import org.bukkit.potion.PotionEffect;
import org.bukkit.potion.PotionEffectType;
import org.bukkit.scheduler.BukkitRunnable;
import org.bukkit.util.EulerAngle;
import org.bukkit.util.Vector;

import java.util.HashSet;
import java.util.Set;
import java.util.UUID;

/**
 * untitled (slash) 플러그인 검기·불꽃 방패 — 보스용 (LivingEntity 시전).
 */
public final class UntitledSlashSkills {

    private static final String WAVE_MARKER = "SWORD_WAVE";
    private static final int WAVE_CUSTOM_MODEL_DATA = 1001;

    private UntitledSlashSkills() {
    }

    public static boolean cast(
            SkeBossPlugin plugin,
            SkeBoss boss,
            String untitledSkill,
            String attackVariable,
            double attackFallback
    ) {
        LivingEntity caster = boss.getEntity();
        if (!caster.isValid() || caster.isDead()) {
            return false;
        }

        return switch (untitledSkill.toLowerCase()) {
            case "slash-wave", "slash", "검기", "불꽃검기" -> {
                double attack = plugin.getBossManager().getSkriptBridge().getEntityStat(
                        caster, attackVariable, attackFallback
                );
                shootSwordAura(plugin, caster, attack);
                yield true;
            }
            case "fire-shield", "shield", "방패", "불꽃방패" -> {
                castFireShield(plugin, caster);
                yield true;
            }
            default -> false;
        };
    }

    private static ItemStack createWaveVisual() {
        ItemStack sword = new ItemStack(Material.FLINT);
        ItemMeta meta = sword.getItemMeta();
        if (meta != null) {
            meta.setCustomModelData(WAVE_CUSTOM_MODEL_DATA);
            sword.setItemMeta(meta);
        }
        return sword;
    }

    private static void equipWaveVisual(ArmorStand wave) {
        if (wave.getEquipment() == null) {
            return;
        }
        ItemStack current = wave.getEquipment().getItemInMainHand();
        if (current.getType() != Material.FLINT
                || !current.hasItemMeta()
                || current.getItemMeta().getCustomModelData() != WAVE_CUSTOM_MODEL_DATA) {
            wave.getEquipment().setItemInMainHand(createWaveVisual());
        }
    }

    private static void shootSwordAura(SkeBossPlugin plugin, LivingEntity caster, double scriptPower) {
        final double finalDamage = scriptPower * 1.5 + 5.0;

        Location startLoc = caster.getEyeLocation();
        final Vector direction = startLoc.getDirection().normalize();
        final float yaw = startLoc.getYaw();
        final float pitch = startLoc.getPitch();

        startLoc.getWorld().playSound(startLoc, Sound.ENTITY_BLAZE_SHOOT, 1.0f, 0.8f);
        startLoc.getWorld().playSound(startLoc, Sound.ENTITY_GENERIC_EXPLODE, 0.5f, 1.5f);

        Location center = caster.getLocation().add(0.0, 1.0, 0.0);
        center.getWorld().spawnParticle(Particle.FLAME, center, 60, 0.5, 0.5, 0.5, 0.3);
        center.getWorld().spawnParticle(Particle.LAVA, center, 15, 0.3, 0.3, 0.3, 0.1);

        final ArmorStand wave = startLoc.getWorld().spawn(startLoc, ArmorStand.class, armorStand -> {
            armorStand.setVisible(false);
            armorStand.setGravity(false);
            armorStand.setArms(true);
            armorStand.setBasePlate(false);
            armorStand.setInvulnerable(true);
            armorStand.setCanPickupItems(false);
            armorStand.setSmall(true);
            armorStand.getEquipment().setItemInMainHand(createWaveVisual());
            armorStand.setCustomName(WAVE_MARKER);
            armorStand.setCustomNameVisible(false);
        });

        new BukkitRunnable() {
            int ticks = 0;
            private final Set<UUID> hitList = new HashSet<>();

            @Override
            public void run() {
                ticks++;
                if (ticks > 40 || wave.isDead() || !wave.isValid()) {
                    removeWave(wave);
                    cancel();
                    return;
                }

                Location currentLoc = wave.getLocation();
                currentLoc.add(direction.clone().multiply(0.8));
                currentLoc.setYaw(yaw);
                currentLoc.setPitch(pitch);
                wave.teleport(currentLoc);
                equipWaveVisual(wave);

                double pitchRadians = Math.toRadians(pitch);
                wave.setRightArmPose(new EulerAngle(pitchRadians, 0.0, 0.0));

                currentLoc.getWorld().spawnParticle(
                        Particle.FLAME,
                        currentLoc.clone().add(0.0, 0.5, 0.0),
                        4, 0.2, 0.2, 0.2, 0.02
                );

                for (Entity entity : currentLoc.getWorld().getNearbyEntities(currentLoc, 1.5, 1.5, 1.5)) {
                    if (!(entity instanceof LivingEntity target)) {
                        continue;
                    }
                    if (entity == caster || entity instanceof ArmorStand || hitList.contains(target.getUniqueId())) {
                        continue;
                    }
                    if (target instanceof Player player) {
                        if (player.getGameMode() == GameMode.SPECTATOR
                                || player.getGameMode() == GameMode.CREATIVE) {
                            continue;
                        }
                    }

                    hitList.add(target.getUniqueId());
                    if (caster instanceof Player player) {
                        player.addScoreboardTag("using_skill");
                    }
                    target.damage(finalDamage, caster);
                    if (caster instanceof Player player) {
                        player.removeScoreboardTag("using_skill");
                    }

                    target.getWorld().spawnParticle(
                            Particle.EXPLOSION_LARGE,
                            target.getLocation().add(0.0, 1.0, 0.0),
                            1, 0.0, 0.0, 0.0, 0.0
                    );
                    target.getWorld().playSound(target.getLocation(), Sound.ENTITY_PLAYER_ATTACK_CRIT, 1.0f, 1.2f);
                }
            }
        }.runTaskTimer(plugin, 0L, 1L);
    }

    private static void castFireShield(SkeBossPlugin plugin, LivingEntity caster) {
        caster.addPotionEffect(new PotionEffect(PotionEffectType.DAMAGE_RESISTANCE, 200, 2));
        caster.getWorld().playSound(caster.getLocation(), Sound.ITEM_FIRECHARGE_USE, 1.0f, 1.0f);

        new BukkitRunnable() {
            int ticks = 0;
            double angle = 0.0;

            @Override
            public void run() {
                if (ticks >= 200 || !caster.isValid() || caster.isDead()) {
                    cancel();
                    return;
                }

                angle += 0.2;
                Location loc = caster.getLocation();
                for (int i = 0; i < 3; i++) {
                    double offset = angle + i * (Math.PI * 2.0 / 3.0);
                    double x = Math.cos(offset) * 1.2;
                    double z = Math.sin(offset) * 1.2;
                    loc.add(x, 1.0, z);
                    loc.getWorld().spawnParticle(Particle.FLAME, loc, 0, 0.0, 0.0, 0.0, 0.0);
                    loc.subtract(x, 1.0, z);
                }
                ticks++;
            }
        }.runTaskTimer(plugin, 0L, 1L);
    }

    private static void removeWave(ArmorStand wave) {
        if (wave == null || !wave.isValid()) {
            return;
        }
        if (wave.getEquipment() != null) {
            wave.getEquipment().clear();
        }
        wave.remove();
    }
}
