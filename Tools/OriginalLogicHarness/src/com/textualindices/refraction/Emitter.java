package com.textualindices.refraction;

/* loaded from: /tmp/claude-0/work/apk/classes.dex */
public class Emitter extends CollisionObject {
    public static final int EMPTY = 0;
    private LaserInstance childLaser = null;
    public int color;

    /* JADX INFO: Access modifiers changed from: package-private */
    public Emitter(int x, int y, double angle, int color) {
        this.xLoc = x;
        this.yLoc = y;
        this.angle = angle;
        this.color = color;
        this.type = 7;
    }

    public LaserInstance getChildLaser() {
        return this.childLaser;
    }

    public void setChildLaser(LaserInstance laser) {
        this.childLaser = laser;
    }
}
