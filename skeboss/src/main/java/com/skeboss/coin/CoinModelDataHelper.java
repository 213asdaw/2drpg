package com.skeboss.coin;

import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.meta.ItemMeta;

import java.lang.reflect.Method;
import java.util.List;
import java.util.logging.Level;
import java.util.logging.Logger;

/** 1.21.4+ 문자열 CMD(coin) + 구버전 숫자 CMD — 런타임 reflection */
public final class CoinModelDataHelper {

    private static final Logger LOG = Logger.getLogger("SkeBoss");
    private static final boolean STRING_CMD_SUPPORTED = detectStringCmdSupport();

    private CoinModelDataHelper() {
    }

    public static boolean supportsStringModelData() {
        return STRING_CMD_SUPPORTED;
    }

    public static void apply(ItemStack item, String modelString, int modelInt) {
        if (item == null) {
            return;
        }
        ItemMeta meta = item.getItemMeta();
        if (meta == null) {
            return;
        }
        boolean hasString = modelString != null && !modelString.isBlank();
        if (STRING_CMD_SUPPORTED && hasString && applyStringAndFloat(meta, modelString, modelInt)) {
            item.setItemMeta(meta);
            return;
        }
        if (STRING_CMD_SUPPORTED && !hasString && modelInt != 0 && applyStringAndFloat(meta, null, modelInt)) {
            item.setItemMeta(meta);
            return;
        }
        if (modelInt != 0) {
            meta.setCustomModelData(modelInt);
            item.setItemMeta(meta);
        }
    }

    public static boolean matches(ItemMeta meta, String modelString, int modelInt) {
        if (meta == null) {
            return false;
        }
        if (STRING_CMD_SUPPORTED && readStringModelData(meta, modelString, modelInt)) {
            return true;
        }
        if (modelInt != 0 && meta.hasCustomModelData()) {
            return meta.getCustomModelData() == modelInt;
        }
        return false;
    }

    private static boolean detectStringCmdSupport() {
        try {
            Class.forName("io.papermc.paper.datacomponent.item.CustomModelData");
            return true;
        } catch (ClassNotFoundException ex) {
            return false;
        }
    }

    private static boolean applyStringAndFloat(ItemMeta meta, String modelString, int modelInt) {
        try {
            Class<?> cmdClass = Class.forName("io.papermc.paper.datacomponent.item.CustomModelData");
            Object builder = cmdClass.getMethod("customModelData").invoke(null);
            boolean hasValue = false;
            if (modelString != null && !modelString.isBlank()) {
                builder.getClass().getMethod("addString", String.class).invoke(builder, modelString);
                hasValue = true;
            }
            if (modelInt != 0) {
                builder.getClass().getMethod("addFloat", float.class).invoke(builder, (float) modelInt);
                hasValue = true;
            }
            if (!hasValue) {
                return false;
            }
            Object built = builder.getClass().getMethod("build").invoke(builder);
            Method setter = meta.getClass().getMethod("setCustomModelData", cmdClass);
            setter.invoke(meta, built);
            return true;
        } catch (ReflectiveOperationException ex) {
            LOG.log(Level.FINE, "1.21.4 CustomModelData 적용 실패, 숫자 CMD로 폴백", ex);
            return false;
        }
    }

    @SuppressWarnings("unchecked")
    private static boolean readStringModelData(ItemMeta meta, String modelString, int modelInt) {
        try {
            Class<?> cmdClass = Class.forName("io.papermc.paper.datacomponent.item.CustomModelData");
            Method getter = meta.getClass().getMethod("getCustomModelData");
            Object data = getter.invoke(meta);
            if (data == null || !cmdClass.isInstance(data)) {
                return false;
            }
            boolean stringOk = false;
            boolean floatOk = false;
            if (modelString != null && !modelString.isBlank()) {
                List<String> strings = (List<String>) data.getClass().getMethod("strings").invoke(data);
                stringOk = strings.stream().anyMatch(s -> s.equalsIgnoreCase(modelString));
            }
            if (modelInt != 0) {
                List<Float> floats = (List<Float>) data.getClass().getMethod("floats").invoke(data);
                floatOk = floats.stream().anyMatch(f -> f.intValue() == modelInt);
            }
            if (modelString != null && !modelString.isBlank() && modelInt != 0) {
                return stringOk || floatOk;
            }
            if (modelString != null && !modelString.isBlank()) {
                return stringOk;
            }
            return modelInt != 0 && floatOk;
        } catch (ReflectiveOperationException ex) {
            return false;
        }
    }
}
