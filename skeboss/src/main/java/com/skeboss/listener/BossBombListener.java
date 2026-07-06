package com.skeboss.listener;

import com.skeboss.skill.BombBossSkills;
import org.bukkit.entity.Entity;
import org.bukkit.entity.Snowball;
import org.bukkit.event.EventHandler;
import org.bukkit.event.EventPriority;
import org.bukkit.event.Listener;
import org.bukkit.event.entity.ProjectileHitEvent;
import org.bukkit.projectiles.ProjectileSource;

public final class BossBombListener implements Listener {

    @EventHandler(priority = EventPriority.HIGH, ignoreCancelled = true)
    public void onProjectileHit(ProjectileHitEvent event) {
        if (!(event.getEntity() instanceof Snowball snowball)) {
            return;
        }
        if (!BombBossSkills.PROJECTILE_MARKER.equals(snowball.getCustomName())) {
            return;
        }
        BombBossSkills.PendingBomb pending = BombBossSkills.peekPending(snowball.getUniqueId());
        if (pending == null) {
            return;
        }

        Entity hit = event.getHitEntity();
        if (hit != null && isShooter(hit, snowball.getShooter(), pending.casterId())) {
            BombBossSkills.clearPending(snowball.getUniqueId());
            snowball.remove();
            return;
        }

        pending = BombBossSkills.removePending(snowball.getUniqueId());
        BombBossSkills.detonateProjectile(snowball, pending);
    }

    private static boolean isShooter(Entity hit, ProjectileSource shooter, java.util.UUID casterId) {
        if (hit.getUniqueId().equals(casterId)) {
            return true;
        }
        if (shooter instanceof Entity shooterEntity) {
            return hit.getUniqueId().equals(shooterEntity.getUniqueId());
        }
        return false;
    }
}
