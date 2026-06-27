package com.skeboss.listener;

import com.skeboss.boss.ModelEngineBossService;
import com.skeboss.boss.SkeBoss;
import org.bukkit.entity.LivingEntity;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.entity.EntityDeathEvent;

public final class SkeBossListener implements Listener {

    private final ModelEngineBossService bossService;

    public SkeBossListener(ModelEngineBossService bossService) {
        this.bossService = bossService;
    }

    @EventHandler
    public void onBossDeath(EntityDeathEvent event) {
        LivingEntity entity = event.getEntity();
        if (!bossService.isBoss(entity)) {
            return;
        }

        SkeBoss boss = bossService.getBoss(entity.getUniqueId());
        if (boss != null) {
            bossService.remove(boss);
        }
    }
}
