package org.example12.slash;

import org.bukkit.Location;
import org.bukkit.Material;
import org.bukkit.Particle;
import org.bukkit.Sound;
import org.bukkit.entity.ArmorStand;
import org.bukkit.entity.Entity;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.event.Event;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.block.Action;
import org.bukkit.event.player.PlayerInteractEvent;
import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.meta.ItemMeta;
import org.bukkit.plugin.java.JavaPlugin;
import org.bukkit.potion.PotionEffect;
import org.bukkit.potion.PotionEffectType;
import org.bukkit.scheduler.BukkitRunnable;
import org.bukkit.util.Vector;

import java.lang.reflect.Method;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

public final class Slash extends JavaPlugin implements Listener {

    private final Map<UUID, Long> cooldowns = new HashMap<>();
    private final Map<UUID, Long> shieldCooldowns = new HashMap<>();
    private final Map<UUID, ItemStack> handBackups = new HashMap<>();

    @Override
    public void onEnable() {
        getServer().getPluginManager().registerEvents(this, this);
        getLogger().info("폭풍의 검 (검기 + 불꽃 방패 + 스탯 연동) 플러그인이 켜졌습니다!");
    }

    private double getScriptAttackPower(Player player) {
        String varName = "공격력::" + player.getUniqueId();
        try {
            Class<?> skriptVars = Class.forName("ch.njol.skript.variables.Variables");
            Method getVar = skriptVars.getMethod("getVariable", String.class, Event.class, boolean.class);
            Object skriptVar = getVar.invoke(null, varName, null, false);
            if (skriptVar instanceof Number number) {
                return number.doubleValue();
            }
        } catch (Exception ignored) {
        }
        return 0.0;
    }

    @EventHandler
    public void onPlayerInteract(PlayerInteractEvent event) {
        Player player = event.getPlayer();
        if (event.getAction() != Action.RIGHT_CLICK_AIR && event.getAction() != Action.RIGHT_CLICK_BLOCK) {
            return;
        }

        ItemStack item = player.getInventory().getItemInMainHand();
        if (item.getType() != Material.DIAMOND_SWORD || !item.hasItemMeta()) {
            return;
        }

        ItemMeta meta = item.getItemMeta();
        if (meta == null || !meta.hasDisplayName() || !meta.getDisplayName().equals("불꽃의 검")) {
            return;
        }

        long currentTime = System.currentTimeMillis();
        if (player.isSneaking()) {
            if (shieldCooldowns.containsKey(player.getUniqueId())) {
                long timeLeft = shieldCooldowns.get(player.getUniqueId()) - currentTime;
                if (timeLeft > 0) {
                    player.sendMessage("§c방패 스킬 쿨타임: " + String.format("%.1f", timeLeft / 1000.0) + "초");
                    player.playSound(player.getLocation(), Sound.ENTITY_EXPERIENCE_ORB_PICKUP, 0.5f, 0.5f);
                    return;
                }
            }
            castFireShield(player);
            shieldCooldowns.put(player.getUniqueId(), currentTime + 15_000L);
            return;
        }

        if (cooldowns.containsKey(player.getUniqueId())) {
            long timeLeft = cooldowns.get(player.getUniqueId()) - currentTime;
            if (timeLeft > 0) {
                player.sendMessage("§c검기 스킬 쿨타임: " + String.format("%.1f", timeLeft / 1000.0) + "초");
                player.playSound(player.getLocation(), Sound.ENTITY_EXPERIENCE_ORB_PICKUP, 0.5f, 0.5f);
                return;
            }
        }

        shootSwordAura(player, item.clone());
        cooldowns.put(player.getUniqueId(), currentTime + 5_000L);
    }

    /** 검기 전 손 무기 백업 — FLINT로 바뀌면 복구 */
    private void backupHand(Player player, ItemStack hand) {
        handBackups.put(player.getUniqueId(), hand.clone());
        UUID id = player.getUniqueId();
        for (long delay : new long[]{1L, 2L, 5L, 10L, 20L, 40L, 60L}) {
            getServer().getScheduler().runTaskLater(this, () -> restoreHandIfFlint(player, id), delay);
        }
    }

    private void restoreHandIfFlint(Player player, UUID id) {
        if (!player.isOnline()) {
            handBackups.remove(id);
            return;
        }
        ItemStack backup = handBackups.get(id);
        if (backup == null) {
            return;
        }
        ItemStack current = player.getInventory().getItemInMainHand();
        if (current.getType() != Material.FLINT) {
            if (current.getType() == backup.getType()) {
                handBackups.remove(id);
            }
            return;
        }
        player.getInventory().setItemInMainHand(backup.clone());
        handBackups.remove(id);
    }

    private static void removeWaveMarker(ArmorStand wave) {
        if (wave == null || !wave.isValid()) {
            return;
        }
        if (wave.getEquipment() != null) {
            wave.getEquipment().clear();
        }
        wave.remove();
    }

    private void castFireShield(Player player) {
        player.addPotionEffect(new PotionEffect(PotionEffectType.DAMAGE_RESISTANCE, 200, 2));
        player.getWorld().playSound(player.getLocation(), Sound.ITEM_FIRECHARGE_USE, 1.0f, 1.0f);
        player.sendMessage("§6🔥 불꽃의 방패가 활성화되었습니다! (10초간 방어력 대폭 상승)");

        new BukkitRunnable() {
            int ticks = 0;
            double angle = 0.0;

            @Override
            public void run() {
                if (ticks >= 200 || !player.isValid()) {
                    player.sendMessage("§c🔥 불꽃의 방패가 꺼졌습니다.");
                    cancel();
                    return;
                }

                angle += 0.2;
                Location loc = player.getLocation();
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
        }.runTaskTimer(this, 0L, 1L);
    }

    /** 검기 — 파티클 + 데미지, 플레이어 손/FLINT 드롭 방지 */
    private void shootSwordAura(Player player, ItemStack savedHand) {
        backupHand(player, savedHand);

        double scriptPower = getScriptAttackPower(player);
        final double finalDamage = scriptPower * 1.5 + 5.0;

        Location startLoc = player.getEyeLocation().clone();
        startLoc.add(startLoc.getDirection().normalize().multiply(0.6));
        final Vector direction = startLoc.getDirection().normalize();
        final float yaw = startLoc.getYaw();
        final float pitch = startLoc.getPitch();

        player.sendMessage("§c🔥 불꽃의 검기");
        startLoc.getWorld().playSound(startLoc, Sound.ENTITY_BLAZE_SHOOT, 1.0f, 0.8f);
        startLoc.getWorld().playSound(startLoc, Sound.ENTITY_GENERIC_EXPLODE, 0.5f, 1.5f);

        Location playerCenter = player.getLocation().add(0.0, 1.0, 0.0);
        playerCenter.getWorld().spawnParticle(Particle.SMALL_FLAME, playerCenter, 60, 0.5, 0.5, 0.5, 0.3);
        playerCenter.getWorld().spawnParticle(Particle.LAVA, playerCenter, 15, 0.3, 0.3, 0.3, 0.1);

        // 히트박스용 invisible 마커 (손에 아이템 없음 — 검기 FLINT 장착 제거)
        final ArmorStand wave = startLoc.getWorld().spawn(startLoc, ArmorStand.class, armorStand -> {
            armorStand.setVisible(false);
            armorStand.setGravity(false);
            armorStand.setArms(false);
            armorStand.setBasePlate(false);
            armorStand.setMarker(true);
            armorStand.setSmall(true);
            armorStand.setCustomName("SWORD_WAVE");
            armorStand.setCustomNameVisible(false);
        });

        new BukkitRunnable() {
            int ticks = 0;
            private final Set<UUID> hitList = new HashSet<>();

            @Override
            public void run() {
                ticks++;
                if (ticks > 40 || wave.isDead() || !wave.isValid()) {
                    removeWaveMarker(wave);
                    cancel();
                    return;
                }

                Location currentLoc = wave.getLocation();
                currentLoc.add(direction.clone().multiply(0.8));
                currentLoc.setYaw(yaw);
                currentLoc.setPitch(pitch);
                wave.teleport(currentLoc);

                currentLoc.getWorld().spawnParticle(
                        Particle.FLAME,
                        currentLoc.clone().add(0.0, 0.5, 0.0),
                        6, 0.25, 0.25, 0.25, 0.02
                );
                currentLoc.getWorld().spawnParticle(
                        Particle.SWEEP_ATTACK,
                        currentLoc.clone().add(0.0, 0.5, 0.0),
                        1, 0.0, 0.0, 0.0, 0.0
                );

                for (Entity entity : currentLoc.getWorld().getNearbyEntities(currentLoc, 1.5, 1.5, 1.5)) {
                    if (!(entity instanceof LivingEntity target)) {
                        continue;
                    }
                    if (entity == player || entity instanceof ArmorStand || hitList.contains(target.getUniqueId())) {
                        continue;
                    }

                    hitList.add(target.getUniqueId());
                    player.addScoreboardTag("using_skill");
                    target.damage(finalDamage, player);
                    player.removeScoreboardTag("using_skill");

                    target.getWorld().spawnParticle(
                            Particle.EXPLOSION_LARGE,
                            target.getLocation().add(0.0, 1.0, 0.0),
                            1, 0.0, 0.0, 0.0, 0.0
                    );
                    target.getWorld().playSound(target.getLocation(), Sound.ENTITY_PLAYER_ATTACK_CRIT, 1.0f, 1.2f);
                }
            }
        }.runTaskTimer(this, 0L, 1L);
    }
}
