package com.skeboss.listener;

import com.skeboss.weapon.WeaponManager;
import org.bukkit.event.Event;
import org.bukkit.event.EventHandler;
import org.bukkit.event.EventPriority;
import org.bukkit.event.Listener;
import org.bukkit.event.block.Action;
import org.bukkit.event.player.PlayerInteractEvent;
import org.bukkit.inventory.EquipmentSlot;
import org.bukkit.inventory.ItemStack;

public final class WeaponListener implements Listener {

    private final WeaponManager weaponManager;

    public WeaponListener(WeaponManager weaponManager) {
        this.weaponManager = weaponManager;
    }

    /**
     * HIGH + ignoreCancelled: 불꽃검기 등 다른 스킬이 먼저 처리한 뒤,
     * 취소되지 않았을 때만 인조 무기 발동.
     */
    @EventHandler(priority = EventPriority.HIGH, ignoreCancelled = true)
    public void onUse(PlayerInteractEvent event) {
        if (!weaponManager.getWeaponConfig().isPluginRightClick()) {
            return;
        }
        if (event.getAction() != Action.RIGHT_CLICK_AIR && event.getAction() != Action.RIGHT_CLICK_BLOCK) {
            return;
        }
        if (event.getHand() != EquipmentSlot.HAND) {
            return;
        }
        if (event.useItemInHand() == Event.Result.DENY) {
            return;
        }

        ItemStack item = event.getItem();
        if (!weaponManager.isInteractWeapon(item)) {
            return;
        }

        event.setCancelled(true);
        event.setUseItemInHand(Event.Result.DENY);
        weaponManager.tryUse(event.getPlayer(), item, event.getPlayer().isSneaking());
    }
}
