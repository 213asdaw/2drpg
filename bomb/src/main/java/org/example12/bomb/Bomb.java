package org.example12.bomb;

import org.bukkit.ChatColor;
import org.bukkit.GameMode;
import org.bukkit.Location;
import org.bukkit.Material;
import org.bukkit.Particle;
import org.bukkit.Sound;
import org.bukkit.entity.Entity;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.entity.Snowball;
import org.bukkit.event.Event;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.block.Action;
import org.bukkit.event.entity.ProjectileHitEvent;
import org.bukkit.event.player.PlayerInteractEvent;
import org.bukkit.event.player.PlayerItemHeldEvent;
import org.bukkit.event.player.PlayerMoveEvent;
import org.bukkit.event.player.PlayerToggleFlightEvent;
import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.meta.ItemMeta;
import org.bukkit.plugin.java.JavaPlugin;
import org.bukkit.util.Vector;

import java.lang.reflect.Method;
import java.util.HashMap;
import java.util.Map;
import java.util.UUID;

public class Bomb extends JavaPlugin implements Listener {

    private static final String BOMB_ITEM_NAME = "폭탄마의 폭탄";
    private static final int PROJECTILE_MODEL_DATA = 1003;

    private final Map<UUID, Long> bombCooldowns = new HashMap<>();
    private final Map<UUID, Long> selfExplodeCooldowns = new HashMap<>();
    private final Map<UUID, Long> doubleJumpCooldowns = new HashMap<>();

    @Override
    public void onEnable() {
        getServer().getPluginManager().registerEvents(this, this);
        getLogger().info("폭탄마 클래스 (폭탄마의 폭탄 / CMD 1003 투사체) 플러그인이 켜졌습니다!");
    }

    private boolean isBombWeapon(ItemStack item) {
        if (item == null || item.getType() != Material.BLAZE_ROD || !item.hasItemMeta()) {
            return false;
        }
        ItemMeta meta = item.getItemMeta();
        if (meta == null || !meta.hasDisplayName()) {
            return false;
        }
        String name = ChatColor.stripColor(meta.getDisplayName()).trim();
        return BOMB_ITEM_NAME.equals(name);
    }

    private ItemStack createBombProjectileVisual() {
        ItemStack visual = new ItemStack(Material.BLAZE_ROD);
        ModelDataHelper.apply(visual, PROJECTILE_MODEL_DATA);
        return visual;
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
    public void onPlayerMove(PlayerMoveEvent event) {
        Player player = event.getPlayer();
        if (player.getGameMode() == GameMode.CREATIVE || player.getGameMode() == GameMode.SPECTATOR) {
            return;
        }
        ItemStack item = player.getInventory().getItemInMainHand();
        if (isBombWeapon(item)) {
            if (player.isOnGround()) {
                player.setAllowFlight(true);
            }
            return;
        }
        if (player.getAllowFlight()) {
            player.setAllowFlight(false);
        }
    }

    @EventHandler
    public void onItemChange(PlayerItemHeldEvent event) {
        Player player = event.getPlayer();
        if (player.getGameMode() != GameMode.CREATIVE && player.getGameMode() != GameMode.SPECTATOR) {
            player.setAllowFlight(false);
        }
    }

    @EventHandler
    public void onPlayerToggleFlight(PlayerToggleFlightEvent event) {
        Player player = event.getPlayer();
        if (player.getGameMode() == GameMode.CREATIVE || player.getGameMode() == GameMode.SPECTATOR) {
            return;
        }
        ItemStack item = player.getInventory().getItemInMainHand();
        if (!isBombWeapon(item)) {
            return;
        }

        event.setCancelled(true);
        player.setAllowFlight(false);
        long currentTime = System.currentTimeMillis();
        if (doubleJumpCooldowns.containsKey(player.getUniqueId())) {
            long timeLeft = doubleJumpCooldowns.get(player.getUniqueId()) - currentTime;
            if (timeLeft > 0L) {
                if (player.getAllowFlight()) {
                    player.setAllowFlight(false);
                }
                player.sendMessage("§c추진 폭발 쿨타임 중입니다! (" + String.format("%.1f", timeLeft / 1000.0) + "초 남음)");
                return;
            }
        }
        if (player.getInventory().containsAtLeast(new ItemStack(Material.GUNPOWDER), 2)) {
            player.getInventory().removeItem(new ItemStack(Material.GUNPOWDER, 2));
            doubleJumpCooldowns.put(player.getUniqueId(), currentTime + 5000L);
            castBlastDash(player);
        } else {
            player.sendMessage("§c⚠ 화약이 부족합니다! (추진 대쉬 필요 화약: 2개)");
        }
    }

    private void castBlastDash(Player player) {
        Location startLoc = player.getLocation();
        startLoc.getWorld().playSound(startLoc, Sound.ENTITY_ITEM_BREAK, 1.0f, 1.5f);
        startLoc.getWorld().playSound(startLoc, Sound.ENTITY_GENERIC_EXPLODE, 0.8f, 1.8f);
        startLoc.getWorld().spawnParticle(Particle.EXPLOSION, startLoc, 3, 0.1, 0.1, 0.1, 0.1);
        double scriptPower = getScriptAttackPower(player);
        double finalDamage = 2.0 + scriptPower * 0.8;
        for (Entity entity : startLoc.getWorld().getNearbyEntities(startLoc, 2.5, 1.5, 2.5)) {
            if (!(entity instanceof LivingEntity target) || entity == player) {
                continue;
            }
            player.addScoreboardTag("using_skill");
            target.damage(finalDamage, player);
            player.removeScoreboardTag("using_skill");
        }
        Vector dashVelocity = player.getLocation().getDirection().multiply(1.4);
        dashVelocity.setY(1.15);
        player.setVelocity(dashVelocity);
        player.sendMessage("§e💨 [추진 폭발] §f발밑을 터뜨려 공중으로 대폭 도약합니다!");
    }

    @EventHandler
    public void onPlayerInteract(PlayerInteractEvent event) {
        Player player = event.getPlayer();
        if (event.getAction() != Action.RIGHT_CLICK_AIR && event.getAction() != Action.RIGHT_CLICK_BLOCK) {
            return;
        }
        ItemStack item = player.getInventory().getItemInMainHand();
        if (!isBombWeapon(item)) {
            return;
        }

        long currentTime = System.currentTimeMillis();
        if (player.isSneaking()) {
            if (selfExplodeCooldowns.containsKey(player.getUniqueId())) {
                long timeLeft = selfExplodeCooldowns.get(player.getUniqueId()) - currentTime;
                if (timeLeft > 0L) {
                    player.sendMessage("§c거대 자폭 쿨타임: " + String.format("%.1f", timeLeft / 1000.0) + "초");
                    return;
                }
            }
            if (player.getInventory().containsAtLeast(new ItemStack(Material.GUNPOWDER), 10)) {
                player.getInventory().removeItem(new ItemStack(Material.GUNPOWDER, 10));
                castSelfExplosion(player);
                selfExplodeCooldowns.put(player.getUniqueId(), currentTime + 12000L);
            } else {
                player.sendMessage("§c⚠ 화약이 부족합니다! (거대 자폭 필요 화약: 10개)");
            }
            return;
        }

        if (bombCooldowns.containsKey(player.getUniqueId())) {
            long timeLeft = bombCooldowns.get(player.getUniqueId()) - currentTime;
            if (timeLeft > 0L) {
                player.sendMessage("§c폭탄 발사 쿨타임: " + String.format("%.1f", timeLeft / 1000.0) + "초");
                return;
            }
        }
        if (player.getInventory().containsAtLeast(new ItemStack(Material.GUNPOWDER), 5)) {
            player.getInventory().removeItem(new ItemStack(Material.GUNPOWDER, 5));
            Snowball bomb = player.launchProjectile(Snowball.class);
            bomb.setItem(createBombProjectileVisual());
            bomb.setVelocity(player.getEyeLocation().getDirection().multiply(1.6));
            bomb.setCustomName("RPG_BOMB");
            player.getWorld().playSound(player.getLocation(), Sound.ENTITY_SNOWBALL_THROW, 1.0f, 0.5f);
            player.sendMessage("§e[폭탄] §f화약 5개를 소모하여 폭탄을 투척했습니다!");
            bombCooldowns.put(player.getUniqueId(), currentTime + 3000L);
        } else {
            player.sendMessage("§c⚠ 화약이 부족합니다! (폭탄 투척 필요 화약: 5개)");
        }
    }

    @EventHandler
    public void onProjectileHit(ProjectileHitEvent event) {
        if (!(event.getEntity() instanceof Snowball projectile)) {
            return;
        }
        if (!"RPG_BOMB".equals(projectile.getCustomName())) {
            return;
        }
        if (projectile.getShooter() instanceof Player player) {
            Location explLoc = projectile.getLocation();
            explLoc.getWorld().playSound(explLoc, Sound.ENTITY_GENERIC_EXPLODE, 2.0f, 0.5f);
            explLoc.getWorld().spawnParticle(Particle.EXPLOSION_EMITTER, explLoc, 5, 2.0, 2.0, 2.0, 0.1);
            explLoc.getWorld().createExplosion(explLoc, 0.0f, true, false);
            double scriptPower = getScriptAttackPower(player);
            double finalDamage = 5.0 + scriptPower * 1.5;
            for (Entity entity : explLoc.getWorld().getNearbyEntities(explLoc, 8.0, 4.0, 8.0)) {
                if (!(entity instanceof LivingEntity target) || entity == player) {
                    continue;
                }
                player.addScoreboardTag("using_skill");
                target.damage(finalDamage, player);
                player.removeScoreboardTag("using_skill");
                Vector push = target.getLocation().toVector().subtract(explLoc.toVector()).normalize().multiply(2.0);
                push.setY(0.8);
                target.setVelocity(push);
            }
            player.sendMessage("§c💣 [쿵-] §f폭탄을 던졌습니다!");
        }
        projectile.remove();
    }

    private void castSelfExplosion(Player player) {
        Location center = player.getLocation();
        center.getWorld().playSound(center, Sound.ENTITY_GENERIC_EXPLODE, 2.0f, 0.5f);
        center.getWorld().spawnParticle(Particle.EXPLOSION_EMITTER, center, 3, 1.0, 1.0, 1.0, 0.1);
        double scriptPower = getScriptAttackPower(player);
        double finalDamage = 10.0 + scriptPower * 2.0;
        for (Entity entity : center.getWorld().getNearbyEntities(center, 6.0, 3.0, 6.0)) {
            if (!(entity instanceof LivingEntity target) || entity == player) {
                continue;
            }
            player.addScoreboardTag("using_skill");
            target.damage(finalDamage, player);
            player.removeScoreboardTag("using_skill");
            Vector push = target.getLocation().toVector().subtract(center.toVector()).normalize().multiply(2.0).setY(0.8);
            target.setVelocity(push);
        }
        player.sendMessage("§c💣 [쿠쿵-] §f거대 자폭 폭발을 일으켰습니다!");
    }
}
