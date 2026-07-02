package com.bomb.util;

import org.bukkit.ChatColor;
import org.bukkit.command.CommandSender;
import org.bukkit.entity.Player;

public final class TextUtil {

    private TextUtil() {
    }

    public static String color(String input) {
        if (input == null) {
            return "";
        }
        return ChatColor.translateAlternateColorCodes('&', input);
    }

    public static String stripColor(String input) {
        if (input == null) {
            return "";
        }
        return ChatColor.stripColor(color(input));
    }

    public static void message(Player player, String message) {
        if (player != null && message != null && !message.isBlank()) {
            player.sendMessage(color(message));
        }
    }

    public static void send(CommandSender sender, String message) {
        if (sender != null && message != null && !message.isBlank()) {
            sender.sendMessage(color(message));
        }
    }
}
