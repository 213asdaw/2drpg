package com.skeboss.cutscene;

import java.util.List;

public final class CutsceneDefinition {

    private final String id;
    private final String description;
    private final List<CutsceneStep> steps;

    public CutsceneDefinition(String id, String description, List<CutsceneStep> steps) {
        this.id = id;
        this.description = description;
        this.steps = List.copyOf(steps);
    }

    public String getId() {
        return id;
    }

    public String getDescription() {
        return description;
    }

    public List<CutsceneStep> getSteps() {
        return steps;
    }
}
