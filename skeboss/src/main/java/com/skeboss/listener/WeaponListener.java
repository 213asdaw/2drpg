package com.skeboss.listener;

import com.skeboss.util.TextUtil;
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
     * 블레이즈 막대(코어)는 Paper가 공중 우클릭을 먼저 막는 경우가 많아
     * LOWEST + ignoreCancelled=false 로 처리한다.
     */
    @EventHandler(priority = EventPriority.LOWEST, ignoreCancelled = false)
    public void onUse(PlayerInteractEvent event) {
        handleInteract(event);
    }

    private void handleInteract(PlayerInteractEvent event) {
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

        if (!weaponManager.tryUse(event.getPlayer(), item, event.getPlayer().isSneaking())) {
            TextUtil.message(event.getPlayer(), "&c[코어] 스킬 시전 실패 — &7/skeboss weapon test");
        }
    }
}
