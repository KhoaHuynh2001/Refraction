package com.textualindices.refraction;

import java.lang.reflect.Array;

/* loaded from: /tmp/claude-0/work/apk/classes.dex */
public class Prism extends CollisionObject {
    public LaserInstance[] slots = new LaserInstance[3];
    public int[] slotsCheck = new int[3];
    public int[][] storedAngles = (int[][]) Array.newInstance((Class<?>) Integer.TYPE, 3, 2);
    public int storedSingleAngle = 360;
    public boolean locked = false;

    public Prism(int x, int y, double angle) {
        this.slotsCheck[0] = 0;
        this.xLoc = x;
        this.yLoc = y;
        this.angle = angle;
        this.type = 2;
        this.storedAngles[1][0] = 361;
        this.storedAngles[1][1] = 7;
        this.storedAngles[2][0] = 361;
        this.storedAngles[2][1] = 7;
    }
}
