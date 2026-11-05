package com.textualindices.refraction;

/* loaded from: /tmp/claude-0/work/apk/classes.dex */
public class Mirror extends CollisionObject {
    boolean isActive;
    public LaserInstance laserIn;
    public LaserInstance laserOut;

    public Mirror(int x, int y, double angle) {
        this.xLoc = x;
        this.yLoc = y;
        this.angle = angle;
        this.isActive = false;
        this.type = 1;
    }
}
