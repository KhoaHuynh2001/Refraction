package com.textualindices.refraction;

/* loaded from: /tmp/claude-0/work/apk/classes.dex */
public class Door extends CollisionObject {
    public static final boolean CLOSED = false;
    public static final boolean OPEN = true;
    private int color;
    public LaserInstance inLaser = null;
    public boolean locked = false;
    private boolean open;

    public Door(int x, int y, int color, boolean openState) {
        this.open = false;
        this.xLoc = x;
        this.yLoc = y;
        this.open = openState;
        this.color = color;
        this.type = 5;
    }

    public String toString() {
        return CalcTools.colorMap(this.color) + " door @ (" + this.xLoc + ", " + this.yLoc + ")";
    }

    public boolean toggleOpenOrClosedchange() {
        if (!this.locked) {
            if (this.open) {
                this.open = false;
            } else {
                this.open = true;
            }
        }
        return !this.locked;
    }

    public boolean setOpenTo(boolean status) {
        if (!this.locked) {
            this.open = status;
        }
        return !this.locked;
    }

    public boolean isOpen() {
        return this.open;
    }

    public boolean isClosed() {
        return !this.open;
    }

    public void setLockedTo(boolean value) {
        this.locked = value;
    }

    public void toggleLocked() {
        this.locked = !this.locked;
    }

    public boolean isUnlocked() {
        return !this.locked;
    }

    public int getColor() {
        return this.color;
    }
}
