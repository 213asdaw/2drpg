package com.skeboss.util;

import org.bukkit.ChatColor;

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
        return ChatColor.stripColor(color(input)).trim();
    }
}
