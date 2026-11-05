package com.textualindices.refraction;

/* loaded from: /tmp/claude-0/work/apk/classes.dex */
public class Switch extends CollisionObject {
    private int color;
    public boolean locked = false;
    public LaserInstance inLaser = null;
    private boolean checked = false;

    public Switch(int x, int y, int color) {
        this.xLoc = x;
        this.yLoc = y;
        this.color = color;
        this.type = 4;
    }

    public String toString() {
        return CalcTools.colorMap(this.color) + " Switch @(" + this.xLoc + "," + this.yLoc + "). Locked=" + this.locked;
    }

    public void setLockedTo(boolean status) {
        this.locked = status;
    }

    public boolean isUnlocked() {
        return !this.locked;
    }

    public boolean isOn() {
        return this.checked;
    }

    public void toggleOnOrOffchange(LaserInstance laser) {
        if (!this.locked) {
            if (!this.checked) {
                this.checked = true;
                this.inLaser = laser;
            } else {
                this.checked = false;
                this.inLaser = null;
            }
        }
    }

    public void toggleOn(LaserInstance laser) {
        if (!this.locked) {
            this.checked = true;
            this.inLaser = laser;
        }
    }

    public void toggleOn() {
        if (!this.locked) {
            this.checked = true;
            this.inLaser = null;
        }
    }

    public void toggleOff() {
        if (!this.locked) {
            this.checked = false;
            this.inLaser = null;
        }
    }

    public int getColor() {
        return this.color;
    }
}
