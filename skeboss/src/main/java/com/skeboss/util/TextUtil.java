package com.skeboss.util;

import net.kyori.adventure.text.serializer.legacy.LegacyComponentSerializer;
import org.bukkit.ChatColor;
import org.bukkit.entity.Player;

public final class TextUtil {

    private static final LegacyComponentSerializer AMPERSAND =
            LegacyComponentSerializer.legacyAmpersand();

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

    public static void actionBar(Player player, String message) {
        if (player == null || message == null || message.isBlank()) {
            return;
        }
        player.sendActionBar(AMPERSAND.deserialize(message));
    }
}
