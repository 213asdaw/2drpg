package com.bomb.listener;

import com.bomb.item.BombManager;
import org.bukkit.entity.Player;
import org.bukkit.entity.Snowball;
import org.bukkit.event.Event;
import org.bukkit.event.EventHandler;
import org.bukkit.event.EventPriority;
import org.bukkit.event.Listener;
import org.bukkit.event.entity.ProjectileHitEvent;
import org.bukkit.event.player.PlayerInteractEvent;
import org.bukkit.inventory.EquipmentSlot;
import org.bukkit.inventory.ItemStack;
import org.bukkit.persistence.PersistentDataType;

public final class BombListener implements Listener {

    private final BombManager bombManager;

    public BombListener(BombManager bombManager) {
        this.bombManager = bombManager;
    }

    @EventHandler(priority = EventPriority.HIGH, ignoreCancelled = false)
    public void onUse(PlayerInteractEvent event) {
        if (event.getAction() != org.bukkit.event.block.Action.RIGHT_CLICK_AIR
                && event.getAction() != org.bukkit.event.block.Action.RIGHT_CLICK_BLOCK) {
            return;
        }
        if (event.getHand() != EquipmentSlot.HAND) {
            return;
        }

        ItemStack item = event.getItem();
        if (item == null || item.getType().isAir()) {
            item = event.getPlayer().getInventory().getItemInMainHand();
        }
        if (!bombManager.isBombItem(item)) {
            return;
        }
        if (bombManager.getConfig().isBlockSneakThrow() && event.getPlayer().isSneaking()) {
            return;
        }

        event.setCancelled(true);
        event.setUseItemInHand(Event.Result.DENY);
        if (event.getAction() == org.bukkit.event.block.Action.RIGHT_CLICK_BLOCK) {
            event.setUseInteractedBlock(Event.Result.DENY);
        }
        bombManager.tryThrow(event.getPlayer(), item);
    }

    @EventHandler(priority = EventPriority.HIGH, ignoreCancelled = true)
    public void onProjectileHit(ProjectileHitEvent event) {
        if (!(event.getEntity() instanceof Snowball snowball)) {
            return;
        }
        if (!snowball.getPersistentDataContainer().has(bombManager.getBombProjectileKey(), PersistentDataType.BYTE)) {
            return;
        }

        Player shooter = null;
        if (snowball.getShooter() instanceof Player player) {
            shooter = player;
        }

        bombManager.explodeBomb(snowball.getLocation(), shooter);
        snowball.remove();
    }
}
