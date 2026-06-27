package com.skeboss.listener;

import com.skeboss.boss.BossManager;
import com.skeboss.boss.SkeBoss;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.entity.Projectile;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.entity.EntityDamageByEntityEvent;
import org.bukkit.event.entity.EntityDeathEvent;
import org.bukkit.event.player.PlayerJoinEvent;
import org.bukkit.event.player.PlayerQuitEvent;
import org.bukkit.event.world.ChunkLoadEvent;

public final class BossListener implements Listener {

    private final BossManager bossManager;

    public BossListener(BossManager bossManager) {
        this.bossManager = bossManager;
    }

    @EventHandler
    public void onBossDamaged(EntityDamageByEntityEvent event) {
        if (!(event.getEntity() instanceof LivingEntity victim) || !bossManager.isBoss(victim)) {
            return;
        }

        Player attacker = resolveAttacker(event.getDamager());
        if (attacker == null) {
            return;
        }

        SkeBoss boss = bossManager.getBoss(victim.getUniqueId());
        if (boss != null) {
            boss.addAggro(attacker);
        }
    }

    private Player resolveAttacker(org.bukkit.entity.Entity damager) {
        if (damager instanceof Player player) {
            return player;
        }
        if (damager instanceof Projectile projectile && projectile.getShooter() instanceof Player shooter) {
            return shooter;
        }
        return null;
    }

    @EventHandler
    public void onDeath(EntityDeathEvent event) {
        LivingEntity entity = event.getEntity();
        if (!bossManager.isBoss(entity)) {
            return;
        }

        event.getDrops().clear();
        event.setDroppedExp(150);

        SkeBoss boss = bossManager.getBoss(entity.getUniqueId());
        if (boss != null) {
            bossManager.remove(boss);
        }
    }

    @EventHandler
    public void onJoin(PlayerJoinEvent event) {
        Player player = event.getPlayer();
        for (SkeBoss boss : bossManager.getBosses()) {
            if (boss.getEntity().getWorld().equals(player.getWorld())
                    && boss.getEntity().getLocation().distanceSquared(player.getLocation()) <= 64 * 64) {
                bossManager.syncBossViewers(boss, player);
            }
        }
    }

    @EventHandler
    public void onQuit(PlayerQuitEvent event) {
        Player player = event.getPlayer();
        for (SkeBoss boss : bossManager.getBosses()) {
            boss.removeViewer(player);
        }
    }

    @EventHandler
    public void onChunkLoad(ChunkLoadEvent event) {
        // 청크 로드 시 보스바 동기화
        for (SkeBoss boss : bossManager.getBosses()) {
            if (!boss.getEntity().getWorld().equals(event.getWorld())) {
                continue;
            }
            if (!event.getChunk().equals(boss.getEntity().getLocation().getChunk())) {
                continue;
            }
            for (Player player : event.getWorld().getPlayers()) {
                if (player.getLocation().distanceSquared(boss.getEntity().getLocation()) <= 64 * 64) {
                    bossManager.syncBossViewers(boss, player);
                }
            }
        }
    }
}
