package com.skeboss.listener;

import com.skeboss.skill.BombBossSkills;
import org.bukkit.entity.Snowball;
import org.bukkit.event.EventHandler;
import org.bukkit.event.EventPriority;
import org.bukkit.event.Listener;
import org.bukkit.event.entity.ProjectileHitEvent;

public final class BossBombListener implements Listener {

    @EventHandler(priority = EventPriority.HIGH, ignoreCancelled = true)
    public void onProjectileHit(ProjectileHitEvent event) {
        if (!(event.getEntity() instanceof Snowball snowball)) {
            return;
        }
        if (!BombBossSkills.PROJECTILE_MARKER.equals(snowball.getCustomName())) {
            return;
        }
        BombBossSkills.PendingBomb pending = BombBossSkills.removePending(snowball.getUniqueId());
        if (pending == null) {
            return;
        }
        BombBossSkills.detonateProjectile(snowball, pending);
    }
}
