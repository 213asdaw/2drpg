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
     * 인조 무기는 공중 우클릭도 허용해야 함.
     * Paper는 블레이즈 막대 등 공중 우클릭을 미리 취소(useItemInHand=DENY)하는 경우가 많아
     * ignoreCancelled=false 로 받고, 인조 무기일 때만 처리한다.
     */
    @EventHandler(priority = EventPriority.HIGH, ignoreCancelled = false)
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

        ItemStack item = event.getItem();
        if (item == null || item.getType().isAir()) {
            item = event.getPlayer().getInventory().getItemInMainHand();
        }
        if (!weaponManager.isInteractWeapon(item)) {
            return;
        }

        event.setCancelled(true);
        event.setUseItemInHand(Event.Result.DENY);
        if (event.getAction() == Action.RIGHT_CLICK_BLOCK) {
            event.setUseInteractedBlock(Event.Result.DENY);
        }
        weaponManager.tryUse(event.getPlayer(), item, event.getPlayer().isSneaking());
    }
}
