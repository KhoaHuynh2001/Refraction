package com.textualindices.refraction;

import java.util.ArrayList;
import java.util.Iterator;

/* loaded from: /tmp/claude-0/work/apk/classes.dex */
public class LaserInstance {
    static final int BLUE = 1;
    static final int DEAD = 0;
    static final int GREEN = 4;
    static final int ORANGE = 5;
    static final int PURPLE = 6;
    static final int RED = 3;
    static final int YELLOW = 2;
    public double angle;
    private int color;
    public int endX;
    public int endY;
    public ArrayList<Prism> parentPrisms;
    public ArrayList<Door> passedDoors;
    public int startX;
    public int startY;
    public boolean endMod = false;
    public CollisionObject parent = null;
    public CollisionObject child = null;
    public boolean hasChild = false;
    public ArrayList<Door> induvidualPassedDoors = new ArrayList<>();

    public LaserInstance(int startX, int startY, double angle, int color, ArrayList<Prism> parentPrisms, ArrayList<Door> passedDoors) {
        this.parentPrisms = null;
        this.passedDoors = null;
        this.startX = startX;
        this.startY = startY;
        this.color = color;
        this.angle = angle;
        this.parentPrisms = parentPrisms;
        this.passedDoors = passedDoors;
    }

    public void setEnd(int endX, int endY) {
        this.endX = endX;
        this.endY = endY;
    }

    public void setParent(CollisionObject parent) {
        this.parent = parent;
    }

    public void setChild(CollisionObject child) {
        this.child = child;
        this.hasChild = true;
    }

    public void purgeCurPassedDoors() {
        Iterator<Door> it = this.induvidualPassedDoors.iterator();
        while (it.hasNext()) {
            Door d = it.next();
            if (this.passedDoors.contains(d)) {
                this.passedDoors.remove(d);
            }
        }
        this.induvidualPassedDoors = new ArrayList<>();
    }

    public int getColor() {
        return this.color;
    }
}
