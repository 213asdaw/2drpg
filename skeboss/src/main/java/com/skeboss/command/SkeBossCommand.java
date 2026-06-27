package com.skeboss.command;

import com.skeboss.boss.ModelEngineBossService;
import com.skeboss.boss.SkeBoss;
import org.bukkit.ChatColor;
import org.bukkit.command.Command;
import org.bukkit.command.CommandExecutor;
import org.bukkit.command.CommandSender;
import org.bukkit.entity.Player;

public final class SkeBossCommand implements CommandExecutor {

    private final ModelEngineBossService bossService;

    public SkeBossCommand(ModelEngineBossService bossService) {
        this.bossService = bossService;
    }

    @Override
    public boolean onCommand(CommandSender sender, Command command, String label, String[] args) {
        if (!(sender instanceof Player player)) {
            sender.sendMessage("플레이어만 사용할 수 있습니다.");
            return true;
        }

        if (args.length == 0) {
            sendUsage(player);
            return true;
        }

        switch (args[0].toLowerCase()) {
            case "spawn" -> {
                try {
                    SkeBoss boss = bossService.spawn(player.getLocation());
                    player.sendMessage(color("&a보스 스폰 완료 (UUID: " + boss.getEntity().getUniqueId() + ")"));
                } catch (IllegalStateException ex) {
                    player.sendMessage(color("&c스폰 실패: " + ex.getMessage()));
                }
            }
            case "skill" -> {
                var target = player.getTargetEntity(20);
                if (!(target instanceof org.bukkit.entity.LivingEntity living) || !bossService.isBoss(living)) {
                    player.sendMessage(color("&c바라보는 보스가 없습니다. /skeboss spawn 후 다시 시도하세요."));
                    return true;
                }
                SkeBoss boss = bossService.getBoss(living.getUniqueId());
                if (bossService.castSkill(boss)) {
                    player.sendMessage(color("&e스킬 시전! 종료 후 idle로 복귀합니다."));
                } else {
                    player.sendMessage(color("&c이미 스킬 시전 중입니다."));
                }
            }
            case "remove" -> {
                var target = player.getTargetEntity(20);
                if (target instanceof org.bukkit.entity.LivingEntity living && bossService.isBoss(living)) {
                    SkeBoss boss = bossService.getBoss(living.getUniqueId());
                    bossService.remove(boss);
                    player.sendMessage(color("&7보스 제거됨"));
                } else {
                    bossService.removeAll();
                    player.sendMessage(color("&7모든 보스 제거됨"));
                }
            }
            default -> sendUsage(player);
        }
        return true;
    }

    private void sendUsage(Player player) {
        player.sendMessage(color("&6/skeboss spawn &7- 보스 스폰"));
        player.sendMessage(color("&6/skeboss skill &7- 바라보는 보스 스킬 (idle 복귀 테스트)"));
        player.sendMessage(color("&6/skeboss remove &7- 보스 제거"));
    }

    private static String color(String message) {
        return ChatColor.translateAlternateColorCodes('&', message);
    }
}
