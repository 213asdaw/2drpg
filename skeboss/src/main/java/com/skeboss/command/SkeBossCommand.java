package com.skeboss.command;

import com.skeboss.boss.BossManager;
import com.skeboss.boss.SkeBoss;
import com.skeboss.boss.SkillDefinition;
import com.skeboss.util.TextUtil;
import org.bukkit.command.Command;
import org.bukkit.command.CommandExecutor;
import org.bukkit.command.CommandSender;
import org.bukkit.command.TabCompleter;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Locale;

public final class SkeBossCommand implements CommandExecutor, TabCompleter {

    private final BossManager bossManager;

    public SkeBossCommand(BossManager bossManager) {
        this.bossManager = bossManager;
    }

    @Override
    public boolean onCommand(CommandSender sender, Command command, String label, String[] args) {
        if (!(sender instanceof Player player)) {
            sender.sendMessage("플레이어만 사용할 수 있습니다.");
            return true;
        }
        if (!player.hasPermission("skeboss.admin")) {
            player.sendMessage(TextUtil.color("&c권한이 없습니다."));
            return true;
        }

        if (args.length == 0) {
            sendHelp(player);
            return true;
        }

        switch (args[0].toLowerCase(Locale.ROOT)) {
            case "spawn" -> handleSpawn(player);
            case "remove", "kill" -> handleRemove(player);
            case "skill" -> handleSkill(player, args);
            case "reload" -> handleReload(player);
            default -> sendHelp(player);
        }
        return true;
    }

    private void handleSpawn(Player player) {
        try {
            SkeBoss boss = bossManager.spawn(player.getLocation());
            player.sendMessage(TextUtil.color("&a해골 보스 스폰 완료! &7(UUID: " + boss.getId() + ")"));
        } catch (IllegalStateException ex) {
            player.sendMessage(TextUtil.color("&c스폰 실패: &f" + ex.getMessage()));
            com.skeboss.SkeBossPlugin.getInstance().getLogger().severe("보스 스폰 실패: " + ex.getMessage());
            if (ex.getCause() != null) {
                ex.getCause().printStackTrace();
            }
        }
    }

    private void handleRemove(Player player) {
        SkeBoss boss = getTargetBoss(player);
        if (boss != null) {
            bossManager.remove(boss);
            player.sendMessage(TextUtil.color("&7보스 1마리 제거했습니다."));
            return;
        }
        int count = bossManager.getBosses().size();
        bossManager.removeAll();
        player.sendMessage(TextUtil.color("&7보스 " + count + "마리 제거했습니다."));
    }

    private void handleSkill(Player player, String[] args) {
        SkeBoss boss = getTargetBoss(player);
        if (boss == null) {
            player.sendMessage(TextUtil.color("&c바라보는 보스가 없습니다."));
            return;
        }

        SkillDefinition skill = bossManager.getConfig().getSkills().stream()
                .filter(def -> args.length < 2 || def.id().equalsIgnoreCase(args[1]))
                .findFirst()
                .orElse(null);

        if (skill == null) {
            player.sendMessage(TextUtil.color("&c스킬을 찾을 수 없습니다. /skeboss skill <이름>"));
            return;
        }

        if (bossManager.castSkill(boss, skill)) {
            player.sendMessage(TextUtil.color("&e스킬 &f" + skill.id() + " &e시전 → 끝나면 idle 복귀"));
        } else {
            player.sendMessage(TextUtil.color("&c스킬 사용 불가 (쿨타임 또는 시전 중)"));
        }
    }

    private void handleReload(Player player) {
        com.skeboss.SkeBossPlugin.getInstance().reloadConfig();
        player.sendMessage(TextUtil.color("&aconfig.yml 리로드 완료. &7(이미 스폰된 보스는 재시작 권장)"));
    }

    private SkeBoss getTargetBoss(Player player) {
        if (player.getTargetEntity(20) instanceof LivingEntity living && bossManager.isBoss(living)) {
            return bossManager.getBoss(living.getUniqueId());
        }
        return null;
    }

    private void sendHelp(Player player) {
        player.sendMessage(TextUtil.color("&6&lSkeBoss 명령어"));
        player.sendMessage(TextUtil.color("&e/skeboss spawn &7- 보스 스폰"));
        player.sendMessage(TextUtil.color("&e/skeboss skill [이름] &7- 스킬 테스트 (idle 복귀)"));
        player.sendMessage(TextUtil.color("&e/skeboss remove &7- 보스 제거"));
        player.sendMessage(TextUtil.color("&e/skeboss reload &7- 설정 리로드"));
    }

    @Override
    public List<String> onTabComplete(CommandSender sender, Command command, String alias, String[] args) {
        if (args.length == 1) {
            return filter(Arrays.asList("spawn", "skill", "remove", "reload"), args[0]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("skill")) {
            List<String> names = bossManager.getConfig().getSkills().stream().map(SkillDefinition::id).toList();
            return filter(names, args[1]);
        }
        return List.of();
    }

    private List<String> filter(List<String> options, String input) {
        String lower = input.toLowerCase(Locale.ROOT);
        List<String> result = new ArrayList<>();
        for (String option : options) {
            if (option.toLowerCase(Locale.ROOT).startsWith(lower)) {
                result.add(option);
            }
        }
        return result;
    }
}
