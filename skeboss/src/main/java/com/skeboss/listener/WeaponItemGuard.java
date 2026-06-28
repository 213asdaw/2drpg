package com.skeboss.listener;

import com.skeboss.SkeBossPlugin;
import com.skeboss.boss.SkeBoss;
import com.skeboss.weapon.WeaponManager;
import org.bukkit.ChatColor;
import org.bukkit.entity.Player;
import org.bukkit.event.EventHandler;
import org.bukkit.event.EventPriority;
import org.bukkit.event.Listener;
import org.bukkit.event.block.Action;
import org.bukkit.event.player.PlayerAnimationEvent;
import org.bukkit.event.player.PlayerAnimationType;
import org.bukkit.event.player.PlayerInteractEvent;
import org.bukkit.event.player.PlayerItemHeldEvent;
import org.bukkit.event.player.PlayerQuitEvent;
import org.bukkit.inventory.EquipmentSlot;
import org.bukkit.inventory.ItemStack;

import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

/**
 * untitled 불꽃검기 등이 손 아이템을 검기로 바꿔놓고 복구 못 할 때 되돌림.
 * 인조인간 보스 근처 전투 시 일반 무기(검 등)도 백업.
 */
public final class WeaponItemGuard implements Listener {

    private final SkeBossPlugin plugin;
    private final WeaponManager weaponManager;
    private final Map<UUID, ItemStack> backups = new ConcurrentHashMap<>();

    public WeaponItemGuard(SkeBossPlugin plugin, WeaponManager weaponManager) {
        this.plugin = plugin;
        this.weaponManager = weaponManager;
    }

    @EventHandler(priority = EventPriority.LOWEST, ignoreCancelled = true)
    public void onInteract(PlayerInteractEvent event) {
        if (event.getHand() != EquipmentSlot.HAND) {
            return;
        }
        Action action = event.getAction();
        if (action != Action.RIGHT_CLICK_AIR && action != Action.RIGHT_CLICK_BLOCK
                && action != Action.LEFT_CLICK_AIR && action != Action.LEFT_CLICK_BLOCK) {
            return;
        }
        tryBackup(event.getPlayer());
    }

    @EventHandler(priority = EventPriority.LOWEST, ignoreCancelled = true)
    public void onSwing(PlayerAnimationEvent event) {
        if (event.getAnimationType() != PlayerAnimationType.ARM_SWING) {
            return;
        }
        tryBackup(event.getPlayer());
    }

    @EventHandler(priority = EventPriority.MONITOR, ignoreCancelled = true)
    public void onSlotChange(PlayerItemHeldEvent event) {
        backups.remove(event.getPlayer().getUniqueId());
    }

    @EventHandler
    public void onQuit(PlayerQuitEvent event) {
        backups.remove(event.getPlayer().getUniqueId());
    }

    private void tryBackup(Player player) {
        if (!weaponManager.getWeaponConfig().isRestoreAfterSkill()) {
            return;
        }

        ItemStack hand = player.getInventory().getItemInMainHand();
        if (!shouldBackup(player, hand)) {
            return;
        }

        backups.put(player.getUniqueId(), hand.clone());
        scheduleRestoreChecks(player);
    }

    private boolean shouldBackup(Player player, ItemStack hand) {
        if (hand == null || hand.getType().isAir()) {
            return false;
        }
        if (weaponManager.isInteractWeapon(hand)) {
            return true;
        }
        if (!weaponManager.getWeaponConfig().isRestoreNearBoss()) {
            return false;
        }
        return isNearBoss(player);
    }

    private boolean isNearBoss(Player player) {
        double radius = weaponManager.getWeaponConfig().getRestoreRadius();
        double radiusSq = radius * radius;
        for (SkeBoss boss : plugin.getBossManager().getBosses()) {
            if (!boss.getEntity().isValid() || boss.getEntity().isDead()) {
                continue;
            }
            if (!boss.getEntity().getWorld().equals(player.getWorld())) {
                continue;
            }
            if (boss.getEntity().getLocation().distanceSquared(player.getLocation()) <= radiusSq) {
                return true;
            }
        }
        return false;
    }

    private void scheduleRestoreChecks(Player player) {
        UUID id = player.getUniqueId();
        for (long delay : new long[]{1L, 3L, 5L, 10L, 20L, 40L, 60L, 80L, 100L}) {
            plugin.getServer().getScheduler().runTaskLater(plugin, () -> tryRestore(player, id), delay);
        }
    }

    private void tryRestore(Player player, UUID id) {
        if (!player.isOnline()) {
            backups.remove(id);
            return;
        }

        ItemStack backup = backups.get(id);
        if (backup == null) {
            return;
        }

        ItemStack current = player.getInventory().getItemInMainHand();
        if (isSameItem(backup, current)) {
            backups.remove(id);
            return;
        }

        if (!shouldRestore(backup, current)) {
            return;
        }

        player.getInventory().setItemInMainHand(backup.clone());
        backups.remove(id);
    }

    private boolean shouldRestore(ItemStack backup, ItemStack current) {
        if (current == null || current.getType().isAir()) {
            return true;
        }

        if (current.getType() == org.bukkit.Material.FLINT) {
            return true;
        }

        String currentName = displayName(current);
        for (String keyword : weaponManager.getWeaponConfig().getRestoreKeywords()) {
            if (!keyword.isBlank() && currentName.contains(keyword)) {
                return true;
            }
        }

        if (current.getType() != backup.getType()) {
            return true;
        }

        String backupName = displayName(backup);
        if (!backupName.isEmpty() && !backupName.equals(currentName)) {
            return looksLikeSkillItem(currentName);
        }

        return weaponManager.getNbtWeaponId(current) == null
                && weaponManager.getNbtWeaponId(backup) != null;
    }

    private boolean looksLikeSkillItem(String name) {
        if (name.isEmpty()) {
            return false;
        }
        List<String> keywords = weaponManager.getWeaponConfig().getRestoreKeywords();
        for (String keyword : keywords) {
            if (!keyword.isBlank() && name.contains(keyword)) {
                return true;
            }
        }
        return false;
    }

    private static boolean isSameItem(ItemStack backup, ItemStack current) {
        if (current == null || backup == null) {
            return false;
        }
        if (current.getType() != backup.getType()) {
            return false;
        }
        return displayName(current).equals(displayName(backup));
    }

    private static String displayName(ItemStack item) {
        if (item == null || !item.hasItemMeta() || !item.getItemMeta().hasDisplayName()) {
            return "";
        }
        return ChatColor.stripColor(item.getItemMeta().getDisplayName());
    }
}
