package com.skeboss.cutscene;

import com.skeboss.modelengine.ModelEngineBridge;
import org.bukkit.Location;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Zombie;

public final class CutsceneActor {

    private final String id;
    private final LivingEntity entity;
    private final ModelEngineBridge.BossModel model;

    public CutsceneActor(String id, LivingEntity entity, ModelEngineBridge.BossModel model) {
        this.id = id;
        this.entity = entity;
        this.model = model;
    }

    public String getId() {
        return id;
    }

    public LivingEntity getEntity() {
        return entity;
    }

    public ModelEngineBridge.BossModel getModel() {
        return model;
    }

    public Location getLocation() {
        return entity.getLocation();
    }

    public boolean isValid() {
        return entity.isValid() && !entity.isDead();
    }

    public void remove(ModelEngineBridge modelEngine) {
        if (model != null) {
            modelEngine.destroy(model);
        }
        if (entity.isValid()) {
            entity.remove();
        }
    }

    public static boolean isCutsceneEntity(LivingEntity entity) {
        return entity instanceof Zombie zombie && zombie.getScoreboardTags().contains(CutsceneManager.ACTOR_TAG);
    }
}
