package com.bomb.command;

import com.bomb.item.BombManager;
import com.bomb.util.TextUtil;
import org.bukkit.Bukkit;
import org.bukkit.command.Command;
import org.bukkit.command.CommandExecutor;
import org.bukkit.command.CommandSender;
import org.bukkit.command.TabCompleter;
import org.bukkit.entity.Player;
import org.bukkit.inventory.ItemStack;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public final class BombCommand implements CommandExecutor, TabCompleter {

    private final BombManager bombManager;

    public BombCommand(BombManager bombManager) {
        this.bombManager = bombManager;
    }

    @Override
    public boolean onCommand(CommandSender sender, Command command, String label, String[] args) {
        if (args.length == 0 || args[0].equalsIgnoreCase("give")) {
            if (!sender.hasPermission("bomb.give")) {
                TextUtil.send(sender, "&c권한이 없습니다.");
                return true;
            }
            Player target = sender instanceof Player player ? player : null;
            int amount = 1;
            if (args.length >= 2 && args[0].equalsIgnoreCase("give")) {
                Player found = Bukkit.getPlayerExact(args[1]);
                if (found != null) {
                    target = found;
                    if (args.length >= 3) {
                        amount = parseAmount(args[2]);
                    }
                } else if (sender instanceof Player) {
                    amount = parseAmount(args[1]);
                }
            }
            if (target == null) {
                TextUtil.send(sender, "&c플레이어를 지정하세요: /bomb give <플레이어> [개수]");
                return true;
            }
            ItemStack item = bombManager.createBombItem(amount);
            target.getInventory().addItem(item);
            TextUtil.send(sender, "&a" + target.getName() + " 에게 폭탄마의 폭탄 &fx" + amount);
            return true;
        }

        if (args[0].equalsIgnoreCase("reload")) {
            if (!sender.hasPermission("bomb.give")) {
                TextUtil.send(sender, "&c권한이 없습니다.");
                return true;
            }
            bombManager.reload();
            TextUtil.send(sender, "&aBomb 설정 리로드 완료");
            return true;
        }

        TextUtil.send(sender, "&e/bomb give [플레이어] [개수] &7| &e/bomb reload");
        return true;
    }

    @Override
    public List<String> onTabComplete(CommandSender sender, Command command, String alias, String[] args) {
        List<String> out = new ArrayList<>();
        if (args.length == 1) {
            if ("give".startsWith(args[0].toLowerCase(Locale.ROOT))) {
                out.add("give");
            }
            if ("reload".startsWith(args[0].toLowerCase(Locale.ROOT))) {
                out.add("reload");
            }
        } else if (args.length == 2 && args[0].equalsIgnoreCase("give")) {
            for (Player player : Bukkit.getOnlinePlayers()) {
                if (player.getName().toLowerCase(Locale.ROOT).startsWith(args[1].toLowerCase(Locale.ROOT))) {
                    out.add(player.getName());
                }
            }
        }
        return out;
    }

    private static int parseAmount(String raw) {
        try {
            return Math.max(1, Math.min(64, Integer.parseInt(raw)));
        } catch (NumberFormatException ex) {
            return 1;
        }
    }
}
