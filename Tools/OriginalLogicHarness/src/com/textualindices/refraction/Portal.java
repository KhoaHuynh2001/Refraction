package com.textualindices.refraction;

/* loaded from: /tmp/claude-0/work/apk/classes.dex */
public class Portal extends CollisionObject {
    public LaserInstance[] laser = new LaserInstance[8];

    public Portal(int x, int y) {
        this.xLoc = x;
        this.yLoc = y;
        this.type = 6;
    }
}
