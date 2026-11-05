package com.textualindices.refraction;

/* loaded from: /tmp/claude-0/work/apk/classes.dex */
public class Checkpoint extends CollisionObject {
    public int color;
    public int checked = 0;
    public LaserInstance inLaser = null;

    public Checkpoint(int x, int y, int color) {
        this.xLoc = x;
        this.yLoc = y;
        this.color = color;
        this.type = 3;
    }
}
