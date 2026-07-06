package com.skeboss.skill;

import com.skeboss.SkeBossPlugin;
import com.skeboss.boss.BossManager;
import com.skeboss.boss.SkeBoss;
import com.skeboss.coin.CoinModelDataHelper;
import org.bukkit.GameMode;
import org.bukkit.Location;
import org.bukkit.Material;
import org.bukkit.Particle;
import org.bukkit.Sound;
import org.bukkit.entity.Entity;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.entity.Snowball;
import org.bukkit.inventory.ItemStack;
import org.bukkit.util.Vector;

import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

/**
 * bomb 플러그인 스킬 — 보스 AI용 (폭탄 투척 / 거대 자폭 / 추진 폭발).
 */
public final class BombBossSkills {

    public static final String PROJECTILE_MARKER = "SKEBOSS_BOMB";
    private static final int PROJECTILE_MODEL_DATA = 1003;

    private static final Map<UUID, PendingBomb> PENDING_BOMBS = new ConcurrentHashMap<>();

    private BombBossSkills() {
    }

    public record PendingBomb(UUID casterId, double damage, double radiusX, double radiusY, double radiusZ) {
    }

    public static boolean cast(
            SkeBossPlugin plugin,
            SkeBoss boss,
            String bombSkill,
            Player aimTarget,
            String attackVariable,
            double attackFallback
    ) {
        LivingEntity caster = boss.getEntity();
        if (!caster.isValid() || caster.isDead()) {
            return false;
        }

        double attack = plugin.getBossManager().getSkriptBridge().getEntityStat(
                caster, attackVariable, attackFallback
        );

        return switch (bombSkill.toLowerCase()) {
            case "bomb-throw", "bomb", "throw", "폭탄", "폭탄투척" -> {
                if (aimTarget == null) {
                    yield false;
                }
                shootBomb(caster, aimTarget, attack);
                yield true;
            }
            case "self-explode", "self-explosion", "explode", "자폭" -> {
                castSelfExplosion(caster, attack);
                yield true;
            }
            case "blast-dash", "dash", "추진", "돌진" -> {
                if (aimTarget == null) {
                    yield false;
                }
                castBlastDash(caster, aimTarget, attack);
                yield true;
            }
            default -> false;
        };
    }

    public static PendingBomb removePending(UUID projectileId) {
        return PENDING_BOMBS.remove(projectileId);
    }

    public static PendingBomb peekPending(UUID projectileId) {
        return PENDING_BOMBS.get(projectileId);
    }

    public static void clearPending(UUID projectileId) {
        PENDING_BOMBS.remove(projectileId);
    }

    private static ItemStack createBombProjectileVisual() {
        ItemStack item = new ItemStack(Material.BLAZE_ROD);
        CoinModelDataHelper.apply(item, null, PROJECTILE_MODEL_DATA);
        return item;
    }

    private static void shootBomb(LivingEntity caster, Player aimTarget, double attack) {
        Location eye = caster.getEyeLocation();
        Vector direction;
        if (aimTarget != null && aimTarget.isValid()) {
            Location aim = aimTarget.getLocation().add(0, aimTarget.getHeight() * 0.5, 0);
            direction = aim.toVector().subtract(eye.toVector());
            if (direction.lengthSquared() < 0.0001) {
                direction = eye.getDirection();
            } else {
                direction.normalize();
            }
            float yaw = (float) Math.toDegrees(Math.atan2(-direction.getX(), direction.getZ()));
            float pitch = (float) -Math.toDegrees(Math.asin(
                    Math.max(-1.0, Math.min(1.0, direction.getY()))
            ));
            caster.setRotation(yaw, pitch);
        } else {
            direction = eye.getDirection().normalize();
        }

        double damage = 5.0 + attack * 1.5;
        Snowball snowball = caster.launchProjectile(Snowball.class);
        snowball.setItem(createBombProjectileVisual());
        snowball.setVelocity(direction.multiply(1.6));
        snowball.setCustomName(PROJECTILE_MARKER);
        snowball.setCustomNameVisible(false);
        snowball.setShooter(caster);

        PENDING_BOMBS.put(snowball.getUniqueId(), new PendingBomb(
                caster.getUniqueId(), damage, 8.0, 4.0, 8.0
        ));

        eye.getWorld().playSound(eye, Sound.ENTITY_SNOWBALL_THROW, 1.0f, 0.5f);
    }

    private static void castSelfExplosion(LivingEntity caster, double attack) {
        Location loc = caster.getLocation();
        double damage = 10.0 + attack * 2.0;
        playExplosionFx(loc, 2.0f, 0.5f, 3);
        damageNearby(caster, loc, damage, 6.0, 3.0, 6.0, true);
    }

    private static void castBlastDash(LivingEntity caster, Player aimTarget, double attack) {
        Location loc = caster.getLocation();
        double damage = 2.0 + attack * 0.8;

        loc.getWorld().playSound(loc, Sound.ENTITY_ITEM_BREAK, 1.0f, 1.5f);
        loc.getWorld().playSound(loc, Sound.ENTITY_GENERIC_EXPLODE, 0.8f, 1.8f);
        loc.getWorld().spawnParticle(Particle.EXPLOSION_LARGE, loc, 3, 0.1, 0.1, 0.1, 0.0);

        damageNearby(caster, loc, damage, 2.5, 1.5, 2.5, false);

        Vector dash;
        if (aimTarget != null && aimTarget.isValid()) {
            dash = aimTarget.getLocation().toVector().subtract(loc.toVector());
            dash.setY(0);
            if (dash.lengthSquared() < 0.0001) {
                dash = loc.getDirection().setY(0);
            }
            dash.normalize().multiply(1.4);
            dash.setY(0.55);
        } else {
            dash = loc.getDirection().multiply(1.4);
            dash.setY(1.15);
        }
        caster.setVelocity(dash);
    }

    public static void detonateProjectile(Snowball snowball, PendingBomb pending) {
        if (snowball == null || pending == null) {
            return;
        }
        Location loc = snowball.getLocation();
        LivingEntity caster = resolveCaster(pending.casterId());
        playExplosionFx(loc, 2.0f, 0.5f, 5);
        if (caster != null) {
            damageNearby(caster, loc, pending.damage(), pending.radiusX(), pending.radiusY(), pending.radiusZ(), true);
        }
        if (snowball.isValid()) {
            snowball.remove();
        }
    }

    private static LivingEntity resolveCaster(UUID casterId) {
        Entity entity = SkeBossPlugin.getInstance().getServer().getEntity(casterId);
        if (entity instanceof LivingEntity living && living.isValid() && !living.isDead()) {
            return living;
        }
        return null;
    }

    private static void playExplosionFx(Location loc, float volume, float pitch, int particles) {
        loc.getWorld().playSound(loc, Sound.ENTITY_GENERIC_EXPLODE, volume, pitch);
        loc.getWorld().spawnParticle(
                Particle.EXPLOSION_LARGE,
                loc,
                particles,
                1.0, 1.0, 1.0,
                0.1
        );
    }

    private static void damageNearby(
            LivingEntity caster,
            Location origin,
            double damage,
            double radiusX,
            double radiusY,
            double radiusZ,
            boolean knockback
    ) {
        UUID casterId = caster.getUniqueId();
        for (Entity entity : origin.getWorld().getNearbyEntities(origin, radiusX, radiusY, radiusZ)) {
            if (!(entity instanceof LivingEntity target)) {
                continue;
            }
            if (shouldSkipBombDamage(casterId, target)) {
                continue;
            }
            target.damage(damage, caster);
            if (knockback) {
                Vector kb = target.getLocation().toVector().subtract(origin.toVector());
                if (kb.lengthSquared() > 0.0001) {
                    kb.normalize().multiply(2.0).setY(0.8);
                    target.setVelocity(kb);
                }
            }
        }
    }

    /** 보스 본인·다른 SkeBoss에는 폭탄 스킬 피해 없음 */
    private static boolean shouldSkipBombDamage(UUID casterId, LivingEntity target) {
        if (target.getUniqueId().equals(casterId)) {
            return true;
        }
        SkeBossPlugin plugin = SkeBossPlugin.getInstance();
        if (plugin == null) {
            return false;
        }
        BossManager manager = plugin.getBossManager();
        if (manager == null) {
            return false;
        }
        if (manager.isBoss(target)) {
            return true;
        }
        if (target instanceof Player player) {
            return player.getGameMode() == GameMode.SPECTATOR
                    || player.getGameMode() == GameMode.CREATIVE;
        }
        return false;
    }
}
