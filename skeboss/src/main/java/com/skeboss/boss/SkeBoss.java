package com.skeboss.boss;

import com.ticxo.modelengine.api.entity.ModeledEntity;
import com.ticxo.modelengine.api.model.ActiveModel;
import org.bukkit.entity.LivingEntity;

public final class SkeBoss {

    private final LivingEntity entity;
    private final ModeledEntity modeledEntity;
    private final ActiveModel activeModel;
    private final BossSettings settings;
    private boolean castingSkill;

    public SkeBoss(
            LivingEntity entity,
            ModeledEntity modeledEntity,
            ActiveModel activeModel,
            BossSettings settings
    ) {
        this.entity = entity;
        this.modeledEntity = modeledEntity;
        this.activeModel = activeModel;
        this.settings = settings;
    }

    public LivingEntity getEntity() {
        return entity;
    }

    public ModeledEntity getModeledEntity() {
        return modeledEntity;
    }

    public ActiveModel getActiveModel() {
        return activeModel;
    }

    public BossSettings getSettings() {
        return settings;
    }

    public boolean isCastingSkill() {
        return castingSkill;
    }

    public void setCastingSkill(boolean castingSkill) {
        this.castingSkill = castingSkill;
    }
}
