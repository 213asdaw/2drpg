package com.skeboss.command;

import com.skeboss.boss.BossManager;
import com.skeboss.boss.SkeBoss;
import com.skeboss.boss.SkillDefinition;
import com.skeboss.util.TextUtil;
import com.skeboss.weapon.WeaponManager;
import org.bukkit.Bukkit;
import org.bukkit.command.Command;
import org.bukkit.command.CommandExecutor;
import org.bukkit.command.CommandSender;
import org.bukkit.command.TabCompleter;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.inventory.ItemStack;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public final class SkeBossCommand implements CommandExecutor, TabCompleter {

    private final BossManager bossManager;
    private final WeaponManager weaponManager;

    public SkeBossCommand(BossManager bossManager, WeaponManager weaponManager) {
        this.bossManager = bossManager;
        this.weaponManager = weaponManager;
    }

    @Override
    public boolean onCommand(CommandSender sender, Command command, String label, String[] args) {
        if (args.length == 0 || args[0].equalsIgnoreCase("help")) {
            sendHelp(sender);
            return true;
        }

        String sub = args[0].toLowerCase(Locale.ROOT);
        switch (sub) {
            case "cast" -> {
                return handleCast(sender, args);
            }
            case "weapon" -> {
                return handleWeapon(sender, args);
            }
            case "spawn" -> {
                return requireAdmin(sender, () -> handleSpawn((Player) sender));
            }
            case "remove", "kill" -> {
                return requireAdmin(sender, () -> handleRemove((Player) sender));
            }
            case "skill" -> {
                return requireAdmin(sender, () -> handleSkill((Player) sender, args));
            }
            case "reload" -> {
                return requireAdmin(sender, () -> handleReload(sender));
            }
            default -> {
                sender.sendMessage(TextUtil.color("&c알 수 없는 명령입니다. &7/skeboss help"));
                return true;
            }
        }
    }

    private boolean requireAdmin(CommandSender sender, Runnable action) {
        if (!(sender instanceof Player player)) {
            sender.sendMessage("플레이어만 사용할 수 있습니다.");
            return true;
        }
        if (!player.hasPermission("skeboss.admin")) {
            player.sendMessage(TextUtil.color("&c권한이 없습니다. &7/skeboss help"));
            return true;
        }
        action.run();
        return true;
    }

    private boolean handleCast(CommandSender sender, String[] args) {
        if (!(sender instanceof Player player)) {
            sender.sendMessage("플레이어만 사용할 수 있습니다.");
            return true;
        }
        if (!player.hasPermission("skeboss.weapon.use")) {
            player.sendMessage(TextUtil.color("&c무기 사용 권한이 없습니다."));
            return true;
        }
        if (args.length < 2) {
            player.sendMessage(TextUtil.color("&c/skeboss cast <laser|chain>"));
            return true;
        }
        weaponManager.castSkill(player, args[1]);
        return true;
    }

    private boolean handleWeapon(CommandSender sender, String[] args) {
        if (!sender.hasPermission("skeboss.weapon.give")) {
            sender.sendMessage(TextUtil.color("&c권한이 없습니다. (skeboss.weapon.give)"));
            return true;
        }
        if (args.length < 2) {
            sender.sendMessage(TextUtil.color("&c/skeboss weapon <이름> [플레이어]"));
            sender.sendMessage(TextUtil.color("&7무기 목록: &f" + String.join(", ", weaponManager.getWeaponConfig().getWeaponIds())));
            return true;
        }

        String weaponId = args[1];
        Player target;
        if (args.length >= 3) {
            target = Bukkit.getPlayer(args[2]);
            if (target == null) {
                sender.sendMessage(TextUtil.color("&c플레이어를 찾을 수 없습니다."));
                return true;
            }
        } else if (sender instanceof Player player) {
            target = player;
        } else {
            sender.sendMessage(TextUtil.color("&c콘솔에서는 플레이어를 지정하세요."));
            return true;
        }

        ItemStack weapon = weaponManager.createWeapon(weaponId);
        if (weapon == null) {
            sender.sendMessage(TextUtil.color("&c무기 없음: &f" + weaponId));
            sender.sendMessage(TextUtil.color("&7목록: &f" + String.join(", ", weaponManager.getWeaponConfig().getWeaponIds())));
            return true;
        }

        target.getInventory().addItem(weapon);
        sender.sendMessage(TextUtil.color("&a무기 지급: &f" + weaponId + " &7→ &f" + target.getName()));
        if (!target.equals(sender)) {
            target.sendMessage(TextUtil.color("&a인조인간 무기를 받았습니다: &f" + weaponId));
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

        boss.addAggro(player);
        boss.setTarget(player);
        bossManager.faceTarget(boss, player);

        if (bossManager.castSkill(boss, skill)) {
            player.sendMessage(TextUtil.color("&e스킬 &f" + skill.id() + " &e시전"));
        } else {
            player.sendMessage(TextUtil.color("&c스킬 사용 불가 (쿨타임 또는 시전 중)"));
        }
    }

    private void handleReload(CommandSender sender) {
        com.skeboss.SkeBossPlugin.getInstance().reloadConfig();
        weaponManager.reload();
        sender.sendMessage(TextUtil.color("&aconfig.yml 리로드 완료. &7(이미 스폰된 보스는 재시작 권장)"));
    }

    private SkeBoss getTargetBoss(Player player) {
        if (player.getTargetEntity(20) instanceof LivingEntity living && bossManager.isBoss(living)) {
            return bossManager.getBoss(living.getUniqueId());
        }
        return null;
    }

    private void sendHelp(CommandSender sender) {
        sender.sendMessage(TextUtil.color("&6&l━━━━ SkeBoss 명령어 ━━━━"));
        if (sender.hasPermission("skeboss.weapon.give")) {
            sender.sendMessage(TextUtil.color("&e/skeboss weapon <이름> [플레이어] &7- 무기 지급"));
            sender.sendMessage(TextUtil.color("&7  무기: &f" + String.join(", ", weaponManager.getWeaponConfig().getWeaponIds())));
        }
        if (sender.hasPermission("skeboss.weapon.use") && sender instanceof Player) {
            sender.sendMessage(TextUtil.color("&e/skeboss cast <laser|chain> &7- 스킬 직접 시전"));
            sender.sendMessage(TextUtil.color("&7  무기 우클릭으로도 사용 가능 (OP 불필요)"));
        }
        if (sender.hasPermission("skeboss.admin")) {
            sender.sendMessage(TextUtil.color("&e/skeboss spawn &7- 보스 스폰"));
            sender.sendMessage(TextUtil.color("&e/skeboss skill [이름] &7- 보스 스킬 테스트"));
            sender.sendMessage(TextUtil.color("&e/skeboss remove &7- 보스 제거"));
            sender.sendMessage(TextUtil.color("&e/skeboss reload &7- 설정 리로드"));
        }
        if (!sender.hasPermission("skeboss.weapon.give")
                && !sender.hasPermission("skeboss.weapon.use")
                && !sender.hasPermission("skeboss.admin")) {
            sender.sendMessage(TextUtil.color("&7사용 가능한 명령이 없습니다."));
        }
    }

    @Override
    public List<String> onTabComplete(CommandSender sender, Command command, String alias, String[] args) {
        if (args.length == 1) {
            List<String> options = new ArrayList<>();
            if (sender.hasPermission("skeboss.admin")) {
                options.addAll(List.of("spawn", "skill", "remove", "reload"));
            }
            if (sender.hasPermission("skeboss.weapon.give")) {
                options.add("weapon");
            }
            if (sender.hasPermission("skeboss.weapon.use") && sender instanceof Player) {
                options.add("cast");
            }
            options.add("help");
            return filter(options, args[0]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("cast") && sender.hasPermission("skeboss.weapon.use")) {
            return filter(List.of("laser", "chain"), args[1]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("skill") && sender.hasPermission("skeboss.admin")) {
            List<String> names = bossManager.getConfig().getSkills().stream().map(SkillDefinition::id).toList();
            return filter(names, args[1]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("weapon") && sender.hasPermission("skeboss.weapon.give")) {
            return filter(weaponManager.getWeaponConfig().getWeaponIds(), args[1]);
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
