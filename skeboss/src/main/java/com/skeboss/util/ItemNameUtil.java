package com.skeboss.util;

import net.kyori.adventure.text.Component;
import net.kyori.adventure.text.serializer.legacy.LegacyComponentSerializer;
import net.kyori.adventure.text.serializer.plain.PlainTextComponentSerializer;
import org.bukkit.ChatColor;
import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.meta.ItemMeta;

import java.util.ArrayList;
import java.util.List;

public final class ItemNameUtil {

    private static final LegacyComponentSerializer LEGACY_SECTION =
            LegacyComponentSerializer.legacySection();
    private static final PlainTextComponentSerializer PLAIN =
            PlainTextComponentSerializer.plainText();

    private ItemNameUtil() {
    }

    public static String plainName(ItemStack item) {
        if (item == null || !item.hasItemMeta()) {
            return "";
        }
        ItemMeta meta = item.getItemMeta();
        if (meta == null) {
            return "";
        }

        String legacy = legacyDisplayName(meta);
        if (!legacy.isBlank()) {
            return legacy;
        }

        String adventure = adventureItemName(meta);
        if (!adventure.isBlank()) {
            return adventure;
        }

        return firstLoreLine(meta);
    }

    public static List<String> plainLore(ItemStack item) {
        if (item == null || !item.hasItemMeta()) {
            return List.of();
        }
        ItemMeta meta = item.getItemMeta();
        if (meta == null) {
            return List.of();
        }
        List<String> lines = new ArrayList<>();
        if (meta.hasLore() && meta.getLore() != null) {
            for (String line : meta.getLore()) {
                if (line == null || line.isBlank()) {
                    continue;
                }
                lines.add(ChatColor.stripColor(line).trim());
            }
        }
        try {
            Object loreComponent = meta.getClass().getMethod("lore").invoke(meta);
            if (loreComponent instanceof List<?> components) {
                for (Object entry : components) {
                    if (entry instanceof Component component) {
                        String plain = PLAIN.serialize(component).trim();
                        if (!plain.isEmpty() && !lines.contains(plain)) {
                            lines.add(plain);
                        }
                    }
                }
            }
        } catch (ReflectiveOperationException ignored) {
        }
        return lines;
    }

    private static String legacyDisplayName(ItemMeta meta) {
        try {
            String raw = meta.getDisplayName();
            if (raw != null && !raw.isBlank()) {
                String stripped = ChatColor.stripColor(raw).trim();
                if (!stripped.isEmpty()) {
                    return stripped;
                }
            }
        } catch (Exception ignored) {
        }
        if (meta.hasDisplayName()) {
            return ChatColor.stripColor(meta.getDisplayName()).trim();
        }
        return "";
    }

    private static String adventureItemName(ItemMeta meta) {
        try {
            if ((boolean) meta.getClass().getMethod("hasItemName").invoke(meta)) {
                Object display = meta.getClass().getMethod("itemName").invoke(meta);
                if (display instanceof Component component) {
                    return PLAIN.serialize(component).trim();
                }
            }
        } catch (ReflectiveOperationException ignored) {
        }

        try {
            Object display = meta.getClass().getMethod("displayName").invoke(meta);
            if (display instanceof Component component) {
                return PLAIN.serialize(component).trim();
            }
        } catch (ReflectiveOperationException ignored) {
        }
        return "";
    }

    private static String firstLoreLine(ItemMeta meta) {
        if (!meta.hasLore() || meta.getLore() == null || meta.getLore().isEmpty()) {
            return "";
        }
        String line = meta.getLore().get(0);
        if (line == null) {
            return "";
        }
        return ChatColor.stripColor(line).trim();
    }

    public static String toLegacyAmpersand(String plain) {
        if (plain == null || plain.isBlank()) {
            return "";
        }
        return LEGACY_SECTION.serialize(Component.text(plain));
    }
}
