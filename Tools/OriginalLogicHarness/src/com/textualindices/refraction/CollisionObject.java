package com.textualindices.refraction;

/* loaded from: /tmp/claude-0/work/apk/classes.dex */
public class CollisionObject {
    public static final int CHECKPOINT = 3;
    public static final int DOOR = 5;
    public static final int EMITTER = 7;
    public static final int MIRROR = 1;
    public static final int PORTAL = 6;
    public static final int PRISM = 2;
    public static final int SWITCH = 4;
    public static final int WALL = 8;
    LaserInstance inLaser;
    public int type;
    public int xLoc;
    public int yLoc;
    public boolean isDummy = false;
    public double angle = 0.0d;
}
