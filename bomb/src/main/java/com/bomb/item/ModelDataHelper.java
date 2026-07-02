package com.bomb.item;

import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.meta.ItemMeta;

import java.lang.reflect.Method;
import java.util.List;
import java.util.logging.Level;
import java.util.logging.Logger;

/** 1.21.4+ float CMD(1003.0) + 구버전 int CMD */
public final class ModelDataHelper {

    private static final Logger LOG = Logger.getLogger("Bomb");
    private static final boolean FLOAT_CMD_SUPPORTED = detectFloatCmdSupport();

    private ModelDataHelper() {
    }

    public static void apply(ItemStack item, int modelInt) {
        if (item == null || modelInt == 0) {
            return;
        }
        ItemMeta meta = item.getItemMeta();
        if (meta == null) {
            return;
        }
        if (FLOAT_CMD_SUPPORTED && applyFloat(meta, modelInt)) {
            item.setItemMeta(meta);
            return;
        }
        meta.setCustomModelData(modelInt);
        item.setItemMeta(meta);
    }

    public static boolean matches(ItemMeta meta, int modelInt) {
        if (meta == null || modelInt == 0) {
            return false;
        }
        if (FLOAT_CMD_SUPPORTED && readFloat(meta, modelInt)) {
            return true;
        }
        return meta.hasCustomModelData() && meta.getCustomModelData() == modelInt;
    }

    private static boolean detectFloatCmdSupport() {
        try {
            Class.forName("io.papermc.paper.datacomponent.item.CustomModelData");
            return true;
        } catch (ClassNotFoundException ex) {
            return false;
        }
    }

    private static boolean applyFloat(ItemMeta meta, int modelInt) {
        try {
            Class<?> cmdClass = Class.forName("io.papermc.paper.datacomponent.item.CustomModelData");
            Object builder = cmdClass.getMethod("customModelData").invoke(null);
            builder.getClass().getMethod("addFloat", float.class).invoke(builder, (float) modelInt);
            Object built = builder.getClass().getMethod("build").invoke(builder);
            Method setter = meta.getClass().getMethod("setCustomModelData", cmdClass);
            setter.invoke(meta, built);
            return true;
        } catch (ReflectiveOperationException ex) {
            LOG.log(Level.FINE, "1.21.4 float CMD 적용 실패", ex);
            return false;
        }
    }

    @SuppressWarnings("unchecked")
    private static boolean readFloat(ItemMeta meta, int modelInt) {
        try {
            Class<?> cmdClass = Class.forName("io.papermc.paper.datacomponent.item.CustomModelData");
            Object data = meta.getClass().getMethod("getCustomModelData").invoke(meta);
            if (data == null || !cmdClass.isInstance(data)) {
                return false;
            }
            List<Float> floats = (List<Float>) data.getClass().getMethod("floats").invoke(data);
            return floats.stream().anyMatch(f -> f.intValue() == modelInt);
        } catch (ReflectiveOperationException ex) {
            return false;
        }
    }
}
