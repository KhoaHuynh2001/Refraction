package com.textualindices.refraction;
import java.util.*;
import java.lang.reflect.Array;

public class OrigSim {
    static int GRID_X_DIM = 8;
    static int GRID_Y_DIM = 10;
    ArrayList<CollisionObject> CheckpointArray = new ArrayList<>(), DoorArray = new ArrayList<>(), EmitterArray = new ArrayList<>(),
        MirrorArray = new ArrayList<>(), ObjectArray = new ArrayList<>(), PortalArray = new ArrayList<>(), PrismArray = new ArrayList<>(),
        SwitchArray = new ArrayList<>(), WallArray = new ArrayList<>(), traceDoorPrisms = new ArrayList<>(), orderPlaceArray = new ArrayList<>(),
        tempPrismStack = new ArrayList<>();
    ArrayList<LaserInstance> LaserArray = new ArrayList<>();
    int curMirror, curPrism, maxMirror = 99, maxPrism = 99;
    boolean alreadyBeat, canRotateAngle = true;
    int[] inDeleteObjLoc = new int[2], tempObjectLoc = new int[2];
    CollisionObject killOutputPrism, tempHolder, tempSelected;
    LaserInstance laserToAngle, previousLaserToAngle;
    int angleSteps = 1, storedAngleSlot = 1, secondStoredAngleSlot = 2;

    private void garbageCollect() {
        ArrayList<LaserInstance> toKill = new ArrayList<>();
        Iterator<LaserInstance> it = this.LaserArray.iterator();
        while (it.hasNext()) {
            LaserInstance laser = it.next();
            boolean hasParent = false;
            Iterator<CollisionObject> it2 = this.ObjectArray.iterator();
            while (it2.hasNext()) {
                CollisionObject spawn = it2.next();
                if (laser.startX == spawn.xLoc && laser.startY == spawn.yLoc) {
                    hasParent = true;
                }
            }
            if (!hasParent) {
                toKill.add(laser);
            }
        }
        Iterator<LaserInstance> it3 = toKill.iterator();
        while (it3.hasNext()) {
            LaserInstance lKill = it3.next();
            killLaserInstance(lKill);
        }
        toKill.clear();
        Iterator<CollisionObject> it4 = this.PrismArray.iterator();
        while (it4.hasNext()) {
            CollisionObject c = it4.next();
            Iterator<LaserInstance> it5 = this.LaserArray.iterator();
            while (it5.hasNext()) {
                LaserInstance laser2 = it5.next();
                if (laser2.startX == c.xLoc && laser2.startY == c.yLoc) {
                    Prism cPrism = (Prism) c;
                    if (laser2 != cPrism.slots[0] && laser2 != cPrism.slots[1] && laser2 != cPrism.slots[2]) {
                        toKill.add(laser2);
                    }
                }
            }
        }
        Iterator<CollisionObject> it6 = this.PortalArray.iterator();
        while (it6.hasNext()) {
            CollisionObject c2 = it6.next();
            Iterator<LaserInstance> it7 = this.LaserArray.iterator();
            while (it7.hasNext()) {
                LaserInstance laser3 = it7.next();
                if (laser3.startX == c2.xLoc && laser3.startY == c2.yLoc) {
                    Portal cPortal = (Portal) c2;
                    Iterator<CollisionObject> it8 = this.PortalArray.iterator();
                    while (it8.hasNext()) {
                        CollisionObject c22 = it8.next();
                        Portal cPortal2 = (Portal) c22;
                        int laserIndexOpposite = ((int) (laser3.angle % 360.0d)) / 45;
                        if (cPortal2 != cPortal && (cPortal2.laser[laserIndexOpposite] == null || laser3.getColor() != cPortal2.laser[laserIndexOpposite].getColor())) {
                            if (!toKill.contains(laser3)) {
                                toKill.add(laser3);
                            }
                        }
                    }
                }
            }
        }
        Iterator<LaserInstance> it9 = toKill.iterator();
        while (it9.hasNext()) {
            LaserInstance lKill2 = it9.next();
            killLaserInstance(lKill2);
        }
    }

    private boolean tracedDoor(CollisionObject startDoor, LaserInstance curLaser) {
        boolean foundChild = false;
        if (curLaser != null) {
            if (curLaser.child != null) {
                if (curLaser.child.type == 1) {
                    Mirror childMirror = (Mirror) curLaser.child;
                    if (childMirror.laserIn == curLaser) {
                        foundChild = tracedDoor(startDoor, childMirror.laserOut);
                    }
                } else if (curLaser.child.type == 2) {
                    Prism childPrism = (Prism) curLaser.child;
                    int inputCount = 0;
                    for (int x = 0; x < 3; x++) {
                        if (childPrism.slotsCheck[x] == 1) {
                            inputCount++;
                        }
                    }
                    if (inputCount == 0) {
                        foundChild = false;
                    } else if (inputCount == 2 && !this.traceDoorPrisms.contains(childPrism) && !curLaser.parentPrisms.contains(childPrism)) {
                        this.traceDoorPrisms.add(childPrism);
                        for (int x2 = 0; x2 < 3; x2++) {
                            if (childPrism.slotsCheck[x2] == 2) {
                                foundChild = tracedDoor(startDoor, childPrism.slots[x2]);
                            }
                        }
                    } else if (!curLaser.parentPrisms.contains(childPrism) && !this.traceDoorPrisms.contains(childPrism)) {
                        this.traceDoorPrisms.add(childPrism);
                        boolean foundChild_1 = false;
                        boolean foundChild_2 = false;
                        if (childPrism.slotsCheck[0] == 1) {
                            foundChild_1 = tracedDoor(startDoor, childPrism.slots[1]);
                            foundChild_2 = tracedDoor(startDoor, childPrism.slots[2]);
                        } else if (childPrism.slotsCheck[1] == 1) {
                            foundChild_1 = tracedDoor(startDoor, childPrism.slots[0]);
                            foundChild_2 = tracedDoor(startDoor, childPrism.slots[2]);
                        } else if (childPrism.slotsCheck[2] == 1) {
                            foundChild_1 = tracedDoor(startDoor, childPrism.slots[0]);
                            foundChild_2 = tracedDoor(startDoor, childPrism.slots[1]);
                        }
                        foundChild = foundChild_1 || foundChild_2;
                    }
                } else if (curLaser.child.type == 6) {
                    Portal childPortal = (Portal) curLaser.child;
                    int laserIndex = ((int) ((curLaser.angle + 180.0d) % 360.0d)) / 45;
                    Iterator<CollisionObject> it = this.PortalArray.iterator();
                    while (it.hasNext()) {
                        CollisionObject p = it.next();
                        if (p != childPortal) {
                            Portal outputPortal = (Portal) p;
                            foundChild = tracedDoor(startDoor, outputPortal.laser[laserIndex]);
                        }
                    }
                } else if (curLaser.child.type == 4) {
                    Switch childSwitch = (Switch) curLaser.child;
                    Door endDoor = (Door) startDoor;
                    foundChild = childSwitch.getColor() == endDoor.getColor() && childSwitch.getColor() == curLaser.getColor();
                } else {
                    foundChild = false;
                }
            } else {
                return false;
            }
        }
        return foundChild;
    }

    private void killCollObject(CollisionObject collObj) {
        ArrayList<LaserInstance> toCalc = new ArrayList<>();
        int xLoc = 0;
        int yLoc = 0;
        if (collObj.type == 1) {
            this.curMirror--;
            Mirror MirrorObject = (Mirror) collObj;
            xLoc = MirrorObject.xLoc;
            yLoc = MirrorObject.yLoc;
            this.ObjectArray.remove(MirrorObject);
            this.orderPlaceArray.remove(MirrorObject);
            if (MirrorObject.isActive) {
                killLaserInstance(MirrorObject.laserOut);
                laserCalcCollision(MirrorObject.laserIn, true);
                MirrorObject.laserIn = null;
                MirrorObject.laserOut = null;
            }
            this.MirrorArray.remove(MirrorObject);
        } else if (collObj.type == 2) {
            this.inDeleteObjLoc[0] = collObj.xLoc;
            this.inDeleteObjLoc[1] = collObj.yLoc;
            this.curPrism--;
            Prism PrismObject = (Prism) collObj;
            xLoc = PrismObject.xLoc;
            yLoc = PrismObject.yLoc;
            this.PrismArray.remove(PrismObject);
            this.ObjectArray.remove(PrismObject);
            this.orderPlaceArray.remove(PrismObject);
            for (int x = 0; x < 3; x++) {
                if (PrismObject.slotsCheck[x] == 2) {
                    killLaserInstance(PrismObject.slots[x]);
                }
            }
            this.inDeleteObjLoc[0] = 0;
            this.inDeleteObjLoc[1] = 0;
        }
        Iterator<CollisionObject> it = this.DoorArray.iterator();
        while (it.hasNext()) {
            CollisionObject d = it.next();
            if (d.xLoc != xLoc || d.yLoc == yLoc) {
            }
        }
        Iterator<LaserInstance> it2 = this.LaserArray.iterator();
        while (it2.hasNext()) {
            LaserInstance l = it2.next();
            if (l.endX == xLoc && l.endY == yLoc) {
                toCalc.add(l);
            }
        }
        Iterator<LaserInstance> it3 = toCalc.iterator();
        while (it3.hasNext()) {
            LaserInstance l2 = it3.next();
            if (this.LaserArray.contains(l2)) {
                laserCalcCollision(l2, true);
            }
        }
    }

    private void killLaserInstance(LaserInstance laser) {
        boolean notEmitterLaser = true;
        if (laser != null) {
            Iterator<CollisionObject> it = this.EmitterArray.iterator();
            while (it.hasNext()) {
                CollisionObject e = it.next();
                Emitter emitter = (Emitter) e;
                if (emitter.color == laser.getColor() && emitter.xLoc == laser.startX && emitter.yLoc == laser.startY && emitter.angle == laser.angle) {
                    notEmitterLaser = false;
                }
            }
        }
        if (laser != null && notEmitterLaser && laser != null) {
            if (laser == this.laserToAngle) {
                this.laserToAngle = null;
            }
            CollisionObject childObject = laser.child;
            this.LaserArray.remove(laser);
            if (childObject != null && childObject.type == 1) {
                Mirror MirrorObject = (Mirror) childObject;
                if (MirrorObject.isActive && laser == MirrorObject.laserIn) {
                    LaserInstance recurLaser = MirrorObject.laserOut;
                    MirrorObject.laserOut = null;
                    MirrorObject.laserIn = null;
                    MirrorObject.isActive = false;
                    killLaserInstance(recurLaser);
                    return;
                }
                return;
            }
            if (childObject != null && childObject.type == 2) {
                Prism PrismObject = (Prism) childObject;
                boolean doStuff = false;
                if ((PrismObject.slotsCheck[0] == 1 && PrismObject.slotsCheck[1] == 1) || ((PrismObject.slotsCheck[0] == 1 && PrismObject.slotsCheck[2] == 1) || (PrismObject.slotsCheck[2] == 1 && PrismObject.slotsCheck[1] == 1))) {
                    int existingOutput = 0;
                    int laserToReCalc = 0;
                    if (laser == PrismObject.slots[0]) {
                        if (PrismObject.slotsCheck[1] == 2) {
                            existingOutput = 1;
                            laserToReCalc = 2;
                        } else {
                            existingOutput = 2;
                            laserToReCalc = 1;
                        }
                        doStuff = true;
                    } else if (laser == PrismObject.slots[1]) {
                        if (PrismObject.slotsCheck[0] == 2) {
                            existingOutput = 0;
                            laserToReCalc = 2;
                        } else {
                            existingOutput = 2;
                            laserToReCalc = 0;
                        }
                        doStuff = true;
                    } else if (laser == PrismObject.slots[2]) {
                        if (PrismObject.slotsCheck[0] == 2) {
                            existingOutput = 0;
                            laserToReCalc = 1;
                        } else {
                            existingOutput = 1;
                            laserToReCalc = 0;
                        }
                        doStuff = true;
                    }
                    if (doStuff) {
                        LaserInstance tempReference = PrismObject.slots[laserToReCalc];
                        killLaserInstance(PrismObject.slots[existingOutput]);
                        for (int x = 0; x < 3; x++) {
                            PrismObject.slotsCheck[x] = 0;
                            PrismObject.slots[x] = null;
                        }
                        laserCalcCollision(tempReference, true);
                        return;
                    }
                    return;
                }
                LaserInstance[] slotsToKill = new LaserInstance[2];
                if (laser == PrismObject.slots[0]) {
                    slotsToKill[0] = PrismObject.slots[1];
                    slotsToKill[1] = PrismObject.slots[2];
                    doStuff = true;
                } else if (laser == PrismObject.slots[1]) {
                    slotsToKill[0] = PrismObject.slots[0];
                    slotsToKill[1] = PrismObject.slots[2];
                    doStuff = true;
                } else if (laser == PrismObject.slots[2]) {
                    slotsToKill[0] = PrismObject.slots[0];
                    slotsToKill[1] = PrismObject.slots[1];
                    doStuff = true;
                }
                if (doStuff) {
                    for (int x2 = 0; x2 < 3; x2++) {
                        PrismObject.slotsCheck[x2] = 0;
                        PrismObject.slots[x2] = null;
                    }
                    killLaserInstance(slotsToKill[0]);
                    killLaserInstance(slotsToKill[1]);
                    return;
                }
                return;
            }
            if (childObject != null && childObject.type == 3) {
                Checkpoint colliderCheckpoint = (Checkpoint) childObject;
                colliderCheckpoint.checked = 0;
                colliderCheckpoint.inLaser = null;
                return;
            }
            if (childObject != null && childObject.type == 4) {
                laser.child = null;
                checkSwitchStatus(childObject);
                return;
            }
            if (childObject != null && childObject.type == 6) {
                Portal colliderPortal = (Portal) childObject;
                int laserIndexOrig = ((int) (laser.angle % 360.0d)) / 45;
                Iterator<CollisionObject> it2 = this.PortalArray.iterator();
                while (it2.hasNext()) {
                    CollisionObject p = it2.next();
                    if (p != colliderPortal) {
                        Portal outputPortal = (Portal) p;
                        int laserIndex = ((int) ((laser.angle + 180.0d) % 360.0d)) / 45;
                        if (outputPortal.laser[laserIndex] != null && laser.getColor() == outputPortal.laser[laserIndex].getColor()) {
                            colliderPortal.laser[laserIndexOrig] = null;
                            killLaserInstance(outputPortal.laser[laserIndex]);
                            outputPortal.laser[laserIndex] = null;
                        }
                    }
                }
            }
        }
    }

    private void laserCalcCollision(LaserInstance tempLaser, boolean produceOutput) {
        int xInc;
        int yInc;
        double newAngle;
        if (tempLaser != null) {
            tempLaser.passedDoors.removeAll(tempLaser.induvidualPassedDoors);
            if (tempLaser.child != null) {
                CollisionObject collisionObject = tempLaser.child;
            }
            tempLaser.hasChild = false;
            tempLaser.endMod = false;
            double angle = tempLaser.angle;
            int startX = tempLaser.startX;
            int startY = tempLaser.startY;
            int color = tempLaser.getColor();
            int xCap = -1;
            int yCap = -1;
            if (angle == 0.0d) {
                xInc = 1;
                yInc = 0;
            } else if (angle == 45.0d) {
                xInc = 1;
                yInc = -1;
            } else if (angle == 90.0d) {
                xInc = 0;
                yInc = -1;
            } else if (angle == 135.0d) {
                xInc = -1;
                yInc = -1;
            } else if (angle == 180.0d) {
                xInc = -1;
                yInc = 0;
            } else if (angle == 225.0d) {
                xInc = -1;
                yInc = 1;
            } else if (angle == 270.0d) {
                xInc = 0;
                yInc = 1;
            } else if (angle == 315.0d) {
                xInc = 1;
                yInc = 1;
            } else {
                xInc = 1;
                yInc = 0;
            }
            int xCur = startX;
            int yCur = startY;
            if (xInc > 0) {
                xCap = GRID_X_DIM + 1;
            }
            if (yInc > 0) {
                yCap = GRID_Y_DIM + 1;
            }
            CollisionObject collider = new Dummy();
            int endX = startX;
            int endY = startY;
            boolean dobreak = false;
            boolean upper = false;
            boolean lower = false;
            boolean upperBack = false;
            boolean lowerBack = false;
            boolean upperDoorOpen = false;
            boolean lowerDoorOpen = false;
            boolean upperBackDoorOpen = false;
            boolean lowerBackDoorOpen = false;
            Door upperDoor = null;
            Door lowerDoor = null;
            Door upperBackDoor = null;
            Door lowerBackDoor = null;
            while (yCur != yCap + yInc && xCur != xCap + xInc && !dobreak) {
                xCur += xInc;
                yCur += yInc;
                Iterator<CollisionObject> it = this.WallArray.iterator();
                while (it.hasNext()) {
                    CollisionObject w = it.next();
                    if (xInc != 0 && yInc != 0) {
                        if (xCur + xInc == w.xLoc && yCur == w.yLoc) {
                            upper = true;
                        } else if (xCur == w.xLoc && yCur + yInc == w.yLoc) {
                            lower = true;
                        } else if (xCur - xInc == w.xLoc && yCur == w.yLoc) {
                            upperBack = true;
                        } else if (xCur == w.xLoc && yCur - yInc == w.yLoc) {
                            lowerBack = true;
                        }
                        if ((upper && lower) || (upperBack && lowerBack)) {
                            dobreak = true;
                        }
                    }
                }
                if (!upper || !lower || !upperBack || !lowerBack) {
                    Iterator<CollisionObject> it2 = this.DoorArray.iterator();
                    while (it2.hasNext()) {
                        CollisionObject d = it2.next();
                        if (xInc != 0 && yInc != 0) {
                            if (xCur + xInc == d.xLoc && yCur == d.yLoc) {
                                if (this.ObjectArray.contains(d)) {
                                    upper = true;
                                }
                                upperDoorOpen = true;
                                upperDoor = (Door) d;
                                if ((upper && lower) || (upperBack && lowerBack)) {
                                    collider = d;
                                    break;
                                }
                            } else if (xCur == d.xLoc && yCur + yInc == d.yLoc) {
                                if (this.ObjectArray.contains(d)) {
                                    lower = true;
                                }
                                lowerDoorOpen = true;
                                lowerDoor = (Door) d;
                                if ((upper && lower) || (upperBack && lowerBack)) {
                                    collider = d;
                                    break;
                                }
                            } else if (xCur - xInc == d.xLoc && yCur == d.yLoc) {
                                if (this.ObjectArray.contains(d)) {
                                    upperBack = true;
                                }
                                upperBackDoorOpen = true;
                                upperBackDoor = (Door) d;
                                if ((upper && lower) || (upperBack && lowerBack)) {
                                    collider = d;
                                    break;
                                }
                            } else if (xCur == d.xLoc && yCur - yInc == d.yLoc) {
                                if (this.ObjectArray.contains(d)) {
                                    lowerBack = true;
                                }
                                lowerBackDoorOpen = true;
                                lowerBackDoor = (Door) d;
                                if ((upper && lower) || (upperBack && lowerBack)) {
                                    collider = d;
                                    break;
                                }
                            }
                        }
                        if (d.xLoc == xCur && d.yLoc == yCur) {
                            Door passedDoor = (Door) d;
                            tempLaser.passedDoors.add(passedDoor);
                            tempLaser.induvidualPassedDoors.add(passedDoor);
                            ArrayList<Door> tempDoorsPassed = (ArrayList) tempLaser.passedDoors.clone();
                            Iterator<CollisionObject> it3 = this.SwitchArray.iterator();
                            while (it3.hasNext()) {
                                CollisionObject s = it3.next();
                                Switch switchS = (Switch) s;
                                if (switchS.getColor() == passedDoor.getColor()) {
                                    Iterator<LaserInstance> it4 = this.LaserArray.iterator();
                                    while (it4.hasNext()) {
                                        LaserInstance l2 = it4.next();
                                        if (l2.getColor() == switchS.getColor() && l2.endX == switchS.xLoc && l2.endY == switchS.yLoc) {
                                            Iterator<Door> it5 = l2.passedDoors.iterator();
                                            while (it5.hasNext()) {
                                                Door d2 = it5.next();
                                                if (!tempDoorsPassed.contains(d2)) {
                                                    tempDoorsPassed.add(d2);
                                                }
                                            }
                                            tempLaser.passedDoors = tempDoorsPassed;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if ((upper && lower) || (upperBack && lowerBack)) {
                    dobreak = true;
                }
                if (upperBack && lowerBackDoorOpen) {
                    tempLaser.passedDoors.add(lowerBackDoor);
                    tempLaser.induvidualPassedDoors.add(lowerBackDoor);
                    ArrayList<Door> tempDoorsPassed2 = (ArrayList) tempLaser.passedDoors.clone();
                    Iterator<CollisionObject> it6 = this.SwitchArray.iterator();
                    while (it6.hasNext()) {
                        CollisionObject s2 = it6.next();
                        Switch switchS2 = (Switch) s2;
                        if (switchS2.getColor() == lowerBackDoor.getColor()) {
                            Iterator<LaserInstance> it7 = this.LaserArray.iterator();
                            while (it7.hasNext()) {
                                LaserInstance l22 = it7.next();
                                if (l22.getColor() == switchS2.getColor() && l22.endX == switchS2.xLoc && l22.endY == switchS2.yLoc) {
                                    Iterator<Door> it8 = l22.passedDoors.iterator();
                                    while (it8.hasNext()) {
                                        Door d3 = it8.next();
                                        if (!tempDoorsPassed2.contains(d3)) {
                                            tempDoorsPassed2.add(d3);
                                        }
                                    }
                                    tempLaser.passedDoors = tempDoorsPassed2;
                                }
                            }
                        }
                    }
                } else if (upperBackDoorOpen && lowerBack) {
                    tempLaser.passedDoors.add(upperBackDoor);
                    tempLaser.induvidualPassedDoors.add(upperBackDoor);
                    ArrayList<Door> tempDoorsPassed3 = (ArrayList) tempLaser.passedDoors.clone();
                    Iterator<CollisionObject> it9 = this.SwitchArray.iterator();
                    while (it9.hasNext()) {
                        CollisionObject s3 = it9.next();
                        Switch switchS3 = (Switch) s3;
                        if (switchS3.getColor() == upperBackDoor.getColor()) {
                            Iterator<LaserInstance> it10 = this.LaserArray.iterator();
                            while (it10.hasNext()) {
                                LaserInstance l23 = it10.next();
                                if (l23.getColor() == switchS3.getColor() && l23.endX == switchS3.xLoc && l23.endY == switchS3.yLoc) {
                                    Iterator<Door> it11 = l23.passedDoors.iterator();
                                    while (it11.hasNext()) {
                                        Door d4 = it11.next();
                                        if (!tempDoorsPassed3.contains(d4)) {
                                            tempDoorsPassed3.add(d4);
                                        }
                                    }
                                    tempLaser.passedDoors = tempDoorsPassed3;
                                }
                            }
                        }
                    }
                } else if (upper && lowerDoorOpen) {
                    tempLaser.passedDoors.add(lowerDoor);
                    tempLaser.induvidualPassedDoors.add(lowerDoor);
                    ArrayList<Door> tempDoorsPassed4 = (ArrayList) tempLaser.passedDoors.clone();
                    Iterator<CollisionObject> it12 = this.SwitchArray.iterator();
                    while (it12.hasNext()) {
                        CollisionObject s4 = it12.next();
                        Switch switchS4 = (Switch) s4;
                        if (switchS4.getColor() == lowerDoor.getColor()) {
                            Iterator<LaserInstance> it13 = this.LaserArray.iterator();
                            while (it13.hasNext()) {
                                LaserInstance l24 = it13.next();
                                if (l24.getColor() == switchS4.getColor() && l24.endX == switchS4.xLoc && l24.endY == switchS4.yLoc) {
                                    Iterator<Door> it14 = l24.passedDoors.iterator();
                                    while (it14.hasNext()) {
                                        Door d5 = it14.next();
                                        if (!tempDoorsPassed4.contains(d5)) {
                                            tempDoorsPassed4.add(d5);
                                        }
                                    }
                                    tempLaser.passedDoors = tempDoorsPassed4;
                                }
                            }
                        }
                    }
                } else if (upperDoorOpen && lower) {
                    tempLaser.passedDoors.add(upperDoor);
                    tempLaser.induvidualPassedDoors.add(upperDoor);
                    ArrayList<Door> tempDoorsPassed5 = (ArrayList) tempLaser.passedDoors.clone();
                    Iterator<CollisionObject> it15 = this.SwitchArray.iterator();
                    while (it15.hasNext()) {
                        CollisionObject s5 = it15.next();
                        Switch switchS5 = (Switch) s5;
                        if (switchS5.getColor() == upperDoor.getColor()) {
                            Iterator<LaserInstance> it16 = this.LaserArray.iterator();
                            while (it16.hasNext()) {
                                LaserInstance l25 = it16.next();
                                if (l25.getColor() == switchS5.getColor() && l25.endX == switchS5.xLoc && l25.endY == switchS5.yLoc) {
                                    Iterator<Door> it17 = l25.passedDoors.iterator();
                                    while (it17.hasNext()) {
                                        Door d6 = it17.next();
                                        if (!tempDoorsPassed5.contains(d6)) {
                                            tempDoorsPassed5.add(d6);
                                        }
                                    }
                                    tempLaser.passedDoors = tempDoorsPassed5;
                                }
                            }
                        }
                    }
                }
                if (!upperBack || !lowerBack) {
                    Iterator<CollisionObject> it18 = this.ObjectArray.iterator();
                    while (true) {
                        if (!it18.hasNext()) {
                            break;
                        }
                        CollisionObject o = it18.next();
                        if (o.xLoc == xCur && o.yLoc == yCur) {
                            if (upper) {
                                tempLaser.passedDoors.remove(upperDoor);
                                tempLaser.induvidualPassedDoors.remove(upperDoor);
                            }
                            if (lower) {
                                tempLaser.passedDoors.remove(lowerDoor);
                                tempLaser.induvidualPassedDoors.remove(lowerDoor);
                            }
                            collider = o;
                            dobreak = true;
                        }
                    }
                }
                endX = xCur;
                endY = yCur;
                if (!dobreak) {
                    if (endX == 0) {
                        endX -= xInc;
                        endY -= yInc;
                    } else if (endY == 0) {
                        endX -= xInc;
                        endY -= yInc;
                    }
                }
                if (!upperBack || lowerBack) {
                }
                if (upper && lower && !gridOccupied(endX, endY)) {
                    endX += xInc;
                    endY += yInc;
                }
                lowerBack = false;
                upperBack = false;
                lower = false;
                upper = false;
            }
            tempLaser.setEnd(endX, endY);
            tempLaser.setChild(collider);
            boolean passedDoorsCheck = true;
            if (collider.type == 2) {
                Iterator<Door> it19 = tempLaser.passedDoors.iterator();
                while (true) {
                    if (!it19.hasNext()) {
                        break;
                    }
                    Door d7 = it19.next();
                    this.traceDoorPrisms.clear();
                    if (tracedDoor(d7, tempLaser)) {
                        passedDoorsCheck = false;
                        this.traceDoorPrisms.clear();
                        break;
                    }
                }
            }
            if (collider.type == 1) {
                tempLaser.endMod = true;
                double[] acceptibleAngle = {CalcTools.angleCorrect(collider.angle - 22.5d), CalcTools.angleCorrect(collider.angle - 45.0d), CalcTools.angleCorrect(collider.angle - 67.5d), CalcTools.angleCorrect(collider.angle - 90.0d), CalcTools.angleCorrect(collider.angle - 112.5d), CalcTools.angleCorrect(collider.angle - 135.0d), CalcTools.angleCorrect(collider.angle - 157.5d)};
                Mirror MirrorCollider = (Mirror) collider;
                if ((tempLaser.angle == acceptibleAngle[0] || tempLaser.angle == acceptibleAngle[1] || tempLaser.angle == acceptibleAngle[2] || tempLaser.angle == acceptibleAngle[3] || tempLaser.angle == acceptibleAngle[4] || tempLaser.angle == acceptibleAngle[5] || tempLaser.angle == acceptibleAngle[6]) && !MirrorCollider.isActive) {
                    double perpAngle = CalcTools.angleCorrect(collider.angle + 90.0d);
                    if (perpAngle < 180.0d) {
                        perpAngle += 180.0d;
                    }
                    double diffAngle = Math.abs(tempLaser.angle - collider.angle);
                    if (diffAngle > 180.0d) {
                        diffAngle = 360.0d - diffAngle;
                    }
                    if (tempLaser.angle == perpAngle) {
                        newAngle = CalcTools.angleCorrect(180.0d + perpAngle);
                    } else {
                        newAngle = CalcTools.angleCorrect(tempLaser.angle + (2.0d * diffAngle));
                    }
                    if (newAngle != perpAngle && newAngle != CalcTools.angleCorrect(perpAngle - 180.0d)) {
                        MirrorCollider.isActive = true;
                        LaserInstance mirrorOutLaser = new LaserInstance(MirrorCollider.xLoc, MirrorCollider.yLoc, newAngle, color, (ArrayList) tempLaser.parentPrisms.clone(), (ArrayList) tempLaser.passedDoors.clone());
                        this.LaserArray.add(mirrorOutLaser);
                        MirrorCollider.laserOut = mirrorOutLaser;
                        MirrorCollider.laserIn = tempLaser;
                        laserCalcCollision(mirrorOutLaser, true);
                        tempLaser.hasChild = true;
                        return;
                    }
                    return;
                }
                return;
            }
            if (collider.type == 2 && !tempLaser.parentPrisms.contains(collider) && passedDoorsCheck && !this.tempPrismStack.contains(collider)) {
                tempLaser.endMod = true;
                Prism colliderPrism = (Prism) collider;
                if (!colliderPrism.locked) {
                    this.tempPrismStack.add(collider);
                    ArrayList<Prism> newParentPrisms = (ArrayList) tempLaser.parentPrisms.clone();
                    newParentPrisms.add(colliderPrism);
                    int[] iArr = new int[2];
                    int secondSlot = 0;
                    if (colliderPrism.slotsCheck[0] == 0 && colliderPrism.slotsCheck[1] == 0 && colliderPrism.slotsCheck[2] == 0) {
                        colliderPrism.slots[0] = tempLaser;
                        colliderPrism.slotsCheck[0] = 1;
                        LaserInstance[] newLasers = new LaserInstance[2];
                        int[] colorOut = CalcTools.splitColor(color);
                        if (colliderPrism.storedAngles[1][1] != 7) {
                            if ((colorOut[0] == colliderPrism.storedAngles[1][1] && colorOut[1] == colliderPrism.storedAngles[2][1]) || (colorOut[1] == colliderPrism.storedAngles[1][1] && colorOut[0] == colliderPrism.storedAngles[2][1])) {
                                if (colorOut[0] == colliderPrism.storedAngles[1][1]) {
                                    newLasers[0] = new LaserInstance(endX, endY, colliderPrism.storedAngles[1][0], colliderPrism.storedAngles[1][1], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                                    colliderPrism.slotsCheck[1] = 2;
                                    secondSlot = 2;
                                } else if (colorOut[0] == colliderPrism.storedAngles[2][1]) {
                                    newLasers[0] = new LaserInstance(endX, endY, colliderPrism.storedAngles[2][0], colliderPrism.storedAngles[2][1], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                                    colliderPrism.slotsCheck[1] = 2;
                                    secondSlot = 1;
                                }
                                newLasers[1] = new LaserInstance(endX, endY, colliderPrism.storedAngles[secondSlot][0], colliderPrism.storedAngles[secondSlot][1], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                                colliderPrism.slotsCheck[2] = 2;
                            } else {
                                double[] outputAngle = CalcTools.prismOutputAngle(tempLaser);
                                newLasers[0] = new LaserInstance(endX, endY, outputAngle[0], colorOut[0], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                                colliderPrism.slotsCheck[1] = 2;
                                newLasers[1] = new LaserInstance(endX, endY, outputAngle[1], colorOut[1], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                                colliderPrism.slotsCheck[2] = 2;
                            }
                        } else {
                            double[] outputAngle2 = CalcTools.prismOutputAngle(tempLaser);
                            newLasers[0] = new LaserInstance(endX, endY, outputAngle2[0], colorOut[0], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                            colliderPrism.slotsCheck[1] = 2;
                            newLasers[1] = new LaserInstance(endX, endY, outputAngle2[1], colorOut[1], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                            colliderPrism.slotsCheck[2] = 2;
                        }
                        if (produceOutput) {
                            colliderPrism.slots[1] = newLasers[0];
                            colliderPrism.slots[2] = newLasers[1];
                            this.LaserArray.add(newLasers[0]);
                            this.LaserArray.add(newLasers[1]);
                            laserCalcCollision(newLasers[0], true);
                            laserCalcCollision(newLasers[1], true);
                        }
                        tempLaser.hasChild = true;
                    } else if ((colliderPrism.slotsCheck[0] != 1 || colliderPrism.slotsCheck[1] != 1) && ((colliderPrism.slotsCheck[0] != 1 || colliderPrism.slotsCheck[2] != 1) && (colliderPrism.slotsCheck[1] != 1 || colliderPrism.slotsCheck[2] != 1))) {
                        boolean childAffectsParent = false;
                        for (int x = 0; x < 3; x++) {
                            if (colliderPrism.slotsCheck[x] == 2) {
                                try {
                                    if ((tempLaser.parentPrisms.contains(colliderPrism.slots[x].child) || this.tempPrismStack.contains(colliderPrism.slots[x].child)) && colliderPrism.slots[x].child.type == 2 && (((Prism) colliderPrism.slots[x].child).slots[0] == colliderPrism.slots[x] || ((Prism) colliderPrism.slots[x].child).slots[1] == colliderPrism.slots[x] || ((Prism) colliderPrism.slots[x].child).slots[2] == colliderPrism.slots[x])) {
                                        childAffectsParent = true;
                                        break;
                                    }
                                } catch (NullPointerException e) {
                                }
                            }
                        }
                        if (!childAffectsParent) {
                            int[] colorIn = new int[2];
                            double[] angleIn = new double[2];
                            int newLaserSlot = 0;
                            colorIn[0] = tempLaser.getColor();
                            angleIn[0] = tempLaser.angle;
                            LaserInstance existingLaserIn = null;
                            boolean splitSingle = false;
                            if (colliderPrism.slotsCheck[0] == 2) {
                                killLaserInstance(colliderPrism.slots[0]);
                                if (colliderPrism.slotsCheck[1] == 2) {
                                    killLaserInstance(colliderPrism.slots[1]);
                                    colliderPrism.slotsCheck[1] = 1;
                                    colliderPrism.slots[1] = tempLaser;
                                    colorIn[1] = colliderPrism.slots[2].getColor();
                                    angleIn[1] = colliderPrism.slots[2].angle;
                                    existingLaserIn = colliderPrism.slots[2];
                                } else {
                                    killLaserInstance(colliderPrism.slots[2]);
                                    colliderPrism.slotsCheck[2] = 1;
                                    colliderPrism.slots[2] = tempLaser;
                                    if (colliderPrism.slots[1] != null) {
                                        colorIn[1] = colliderPrism.slots[1].getColor();
                                        angleIn[1] = colliderPrism.slots[1].angle;
                                        existingLaserIn = colliderPrism.slots[1];
                                    } else {
                                        splitSingle = true;
                                    }
                                }
                                newLaserSlot = 0;
                            } else if (colliderPrism.slotsCheck[1] == 2) {
                                killLaserInstance(colliderPrism.slots[1]);
                                if (colliderPrism.slotsCheck[0] == 2) {
                                    killLaserInstance(colliderPrism.slots[0]);
                                    colliderPrism.slotsCheck[0] = 1;
                                    colliderPrism.slots[0] = tempLaser;
                                    colorIn[1] = colliderPrism.slots[2].getColor();
                                    angleIn[1] = colliderPrism.slots[2].angle;
                                    existingLaserIn = colliderPrism.slots[2];
                                } else {
                                    killLaserInstance(colliderPrism.slots[2]);
                                    colliderPrism.slotsCheck[2] = 1;
                                    colliderPrism.slots[2] = tempLaser;
                                    if (colliderPrism.slots[0] != null) {
                                        colorIn[1] = colliderPrism.slots[0].getColor();
                                        angleIn[1] = colliderPrism.slots[0].angle;
                                        existingLaserIn = colliderPrism.slots[0];
                                    } else {
                                        splitSingle = true;
                                    }
                                }
                                newLaserSlot = 1;
                            }
                            if (splitSingle) {
                                LaserInstance[] newLasers2 = new LaserInstance[2];
                                int[] colorOut2 = CalcTools.splitColor(color);
                                if (colliderPrism.storedAngles[1][1] != 7) {
                                    if ((colorOut2[0] == colliderPrism.storedAngles[1][1] && colorOut2[1] == colliderPrism.storedAngles[2][1]) || (colorOut2[1] == colliderPrism.storedAngles[1][1] && colorOut2[0] == colliderPrism.storedAngles[2][1])) {
                                        if (colorOut2[0] == colliderPrism.storedAngles[1][1]) {
                                            newLasers2[0] = new LaserInstance(endX, endY, colliderPrism.storedAngles[1][0], colliderPrism.storedAngles[1][1], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                                            colliderPrism.slotsCheck[1] = 2;
                                            secondSlot = 2;
                                        } else if (colorOut2[0] == colliderPrism.storedAngles[2][1]) {
                                            newLasers2[0] = new LaserInstance(endX, endY, colliderPrism.storedAngles[2][0], colliderPrism.storedAngles[2][1], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                                            colliderPrism.slotsCheck[1] = 2;
                                            secondSlot = 1;
                                        }
                                        newLasers2[1] = new LaserInstance(endX, endY, colliderPrism.storedAngles[secondSlot][0], colliderPrism.storedAngles[secondSlot][1], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                                        colliderPrism.slotsCheck[2] = 2;
                                    } else {
                                        double[] outputAngle3 = CalcTools.prismOutputAngle(tempLaser);
                                        newLasers2[0] = new LaserInstance(endX, endY, outputAngle3[0], colorOut2[0], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                                        colliderPrism.slotsCheck[1] = 2;
                                        newLasers2[1] = new LaserInstance(endX, endY, outputAngle3[1], colorOut2[1], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                                        colliderPrism.slotsCheck[2] = 2;
                                    }
                                } else {
                                    double[] outputAngle4 = CalcTools.prismOutputAngle(tempLaser);
                                    newLasers2[0] = new LaserInstance(endX, endY, outputAngle4[0], colorOut2[0], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                                    colliderPrism.slotsCheck[1] = 2;
                                    newLasers2[1] = new LaserInstance(endX, endY, outputAngle4[1], colorOut2[1], newParentPrisms, (ArrayList) tempLaser.passedDoors.clone());
                                    colliderPrism.slotsCheck[2] = 2;
                                }
                                if (produceOutput) {
                                    colliderPrism.slots[1] = newLasers2[0];
                                    colliderPrism.slots[2] = newLasers2[1];
                                    this.LaserArray.add(newLasers2[0]);
                                    this.LaserArray.add(newLasers2[1]);
                                    laserCalcCollision(newLasers2[0], true);
                                    laserCalcCollision(newLasers2[1], true);
                                }
                            } else {
                                double angleToUse = CalcTools.prismOutputAngle(tempLaser, angleIn[1]);
                                if (colliderPrism.storedSingleAngle != 360) {
                                    angleToUse = colliderPrism.storedSingleAngle;
                                }
                                ArrayList<Door> doorListCombined = (ArrayList) tempLaser.passedDoors.clone();
                                if (existingLaserIn != null) {
                                    doorListCombined.addAll((ArrayList) existingLaserIn.passedDoors.clone());
                                }
                                if (existingLaserIn != null) {
                                    newParentPrisms.addAll((ArrayList) existingLaserIn.parentPrisms.clone());
                                }
                                LaserInstance singleOutput = new LaserInstance(endX, endY, angleToUse, CalcTools.combineColor(colorIn), newParentPrisms, doorListCombined);
                                this.LaserArray.add(singleOutput);
                                colliderPrism.slots[newLaserSlot] = singleOutput;
                                colliderPrism.slotsCheck[newLaserSlot] = 2;
                                laserCalcCollision(singleOutput, true);
                                colliderPrism.storedSingleAngle = (int) angleToUse;
                                tempLaser.hasChild = true;
                            }
                        }
                    }
                    this.tempPrismStack.remove(collider);
                    return;
                }
                return;
            }
            if (collider.type == 3) {
                tempLaser.endMod = true;
                Checkpoint colliderCheckpoint = (Checkpoint) collider;
                if (tempLaser.getColor() == colliderCheckpoint.color) {
                    colliderCheckpoint.checked = 1;
                    colliderCheckpoint.inLaser = tempLaser;
                    this.alreadyBeat = false;
                    return;
                }
                return;
            }
            if (collider.type == 4) {
                tempLaser.endMod = true;
                checkSwitchStatus(collider);
                return;
            }
            if (collider.type == 5) {
                Door colliderDoor = (Door) collider;
                colliderDoor.inLaser = tempLaser;
                return;
            }
            if (collider.type == 6) {
                tempLaser.endMod = true;
                Portal colliderPortal = (Portal) collider;
                int laserIndex = ((int) (tempLaser.angle % 360.0d)) / 45;
                int laserIndexOpposite = ((int) ((tempLaser.angle + 180.0d) % 360.0d)) / 45;
                if (colliderPortal.laser[laserIndex] == null) {
                    colliderPortal.laser[laserIndex] = tempLaser;
                    Iterator<CollisionObject> it20 = this.PortalArray.iterator();
                    while (it20.hasNext()) {
                        CollisionObject p = it20.next();
                        if (p != colliderPortal) {
                            Portal outputPortal = (Portal) p;
                            LaserInstance tempPortalLaser = new LaserInstance(outputPortal.xLoc, outputPortal.yLoc, tempLaser.angle, tempLaser.getColor(), (ArrayList) tempLaser.parentPrisms.clone(), (ArrayList) tempLaser.passedDoors.clone());
                            outputPortal.laser[laserIndexOpposite] = tempPortalLaser;
                            this.LaserArray.add(tempPortalLaser);
                            laserCalcCollision(tempPortalLaser, true);
                        }
                    }
                    return;
                }
                return;
            }
            if (collider.type == 7) {
                tempLaser.endMod = true;
                Emitter colliderEmitter = (Emitter) collider;
                if (colliderEmitter.color == 0) {
                    activateReactor(colliderEmitter, tempLaser.getColor());
                }
            }
        }
    }

    public CollisionObject newMirror(int X, int Y, double angle) {
        CollisionObject tempMirror = new Mirror(X, Y, angle);
        this.MirrorArray.add(tempMirror);
        this.ObjectArray.add(tempMirror);
        this.orderPlaceArray.add(tempMirror);
        laserSearch(tempMirror, false);
        return tempMirror;
    }

    private CollisionObject newCheckpoint(int x, int y, int color) {
        CollisionObject tempCheck = new Checkpoint(x, y, color);
        this.ObjectArray.add(tempCheck);
        this.CheckpointArray.add(tempCheck);
        laserSearch(tempCheck, false);
        return tempCheck;
    }

    private CollisionObject newSwitch(int x, int y, int color) {
        CollisionObject tempSwitch = new Switch(x, y, color);
        this.ObjectArray.add(tempSwitch);
        this.SwitchArray.add(tempSwitch);
        laserSearch(tempSwitch, false);
        return tempSwitch;
    }

    private CollisionObject newDoor(int x, int y, int color, boolean doorOpen) {
        CollisionObject tempDoor = new Door(x, y, color, doorOpen);
        if (doorOpen) {
            this.ObjectArray.add(tempDoor);
        }
        this.DoorArray.add(tempDoor);
        laserSearch(tempDoor, false);
        return tempDoor;
    }

    private CollisionObject newWall(int x, int y) {
        CollisionObject tempWall = new Wall(x, y);
        this.ObjectArray.add(tempWall);
        this.WallArray.add(tempWall);
        laserSearch(tempWall, false);
        return tempWall;
    }

    private CollisionObject newEmitter(int x, int y, int color, double angle) {
        CollisionObject tempEmitter = new Emitter(x, y, angle, color);
        this.ObjectArray.add(tempEmitter);
        this.EmitterArray.add(tempEmitter);
        laserSearch(tempEmitter, false);
        return tempEmitter;
    }

    private CollisionObject newPortal(int x, int y) {
        CollisionObject tempPortal = new Portal(x, y);
        this.ObjectArray.add(tempPortal);
        this.PortalArray.add(tempPortal);
        laserSearch(tempPortal, false);
        return tempPortal;
    }

    public CollisionObject newPrism(int X, int Y, double angle) {
        CollisionObject tempPrism = new Prism(X, Y, angle);
        this.PrismArray.add(tempPrism);
        this.ObjectArray.add(tempPrism);
        this.orderPlaceArray.add(tempPrism);
        laserSearch(tempPrism, false);
        return tempPrism;
    }

    public CollisionObject newReactor(int x, int y, double angle) {
        CollisionObject tempEmitter = new Emitter(x, y, angle, 0);
        this.ObjectArray.add(tempEmitter);
        this.EmitterArray.add(tempEmitter);
        laserSearch(tempEmitter, false);
        return tempEmitter;
    }

    public void newLaserInstance(int xCoordinate, int yCoordinate, int laserAngle, int laserColor) {
        LaserInstance laser = new LaserInstance(xCoordinate, yCoordinate, laserAngle, laserColor, new ArrayList(), new ArrayList());
        this.LaserArray.add(laser);
        laserCalcCollision(laser, true);
    }

    public boolean gridOccupied(int x, int y) {
        Iterator<CollisionObject> it = this.ObjectArray.iterator();
        while (it.hasNext()) {
            CollisionObject object = it.next();
            if (object.yLoc == y && object.xLoc == x) {
                return true;
            }
        }
        return false;
    }

    private void laserSearch(CollisionObject collider, boolean killOutput) {
        int cX = collider.xLoc;
        int cY = collider.yLoc;
        ArrayList<LaserInstance> toCalc = new ArrayList<>();
        Iterator<LaserInstance> it = this.LaserArray.iterator();
        while (it.hasNext()) {
            LaserInstance l = it.next();
            if (l.endX != -1) {
                int endX = l.endX;
            }
            if (l.endY != -1) {
                int i = l.endY;
            }
            int endX2 = l.endX;
            int endY = l.endY;
            if (l.startX != l.endX) {
                if ((cY - l.startY) * (-1) == (cX - l.startX) * (((endY - l.startY) * (-1)) / (endX2 - l.startX)) && CalcTools.inBetween(cX, l.startX, endX2) && CalcTools.inBetween(cY, l.startY, endY)) {
                    toCalc.add(l);
                }
            } else if (cX == l.startX && CalcTools.inBetween(cY, l.startY, endY)) {
                toCalc.add(l);
            }
            if (l.child == collider && !toCalc.contains(l)) {
                toCalc.add(l);
            }
            if (collider.type == 5 && l.startX != l.endX) {
                int lSlope = ((endY - l.startY) * (-1)) / (endX2 - l.startX);
                Door doorCollider = (Door) collider;
                if (doorCollider.isClosed()) {
                    ArrayList<CollisionObject> tempWallDoorArray = (ArrayList) this.WallArray.clone();
                    tempWallDoorArray.addAll(this.DoorArray);
                    Iterator<CollisionObject> it2 = tempWallDoorArray.iterator();
                    while (it2.hasNext()) {
                        CollisionObject w = it2.next();
                        boolean canCheck = true;
                        if (w.type == 5) {
                            Door testDoor = (Door) w;
                            canCheck = testDoor.isClosed() && testDoor != doorCollider;
                        }
                        if (canCheck) {
                            if (doorCollider.xLoc + 1 == w.xLoc && doorCollider.yLoc + 1 == w.yLoc && ((cY + 1) - l.startY) * (-1) == (cX - l.startX) * lSlope && CalcTools.inBetween(cX, l.startX, endX2) && CalcTools.inBetween(cY + 1, l.startY, endY) && (cY - l.startY) * (-1) == ((cX + 1) - l.startX) * lSlope && CalcTools.inBetween(cX + 1, l.startX, endX2) && CalcTools.inBetween(cY, l.startY, endY) && !toCalc.contains(l)) {
                                toCalc.add(l);
                            }
                            if (doorCollider.xLoc - 1 == w.xLoc && doorCollider.yLoc - 1 == w.yLoc && ((cY - 1) - l.startY) * (-1) == (cX - l.startX) * lSlope && CalcTools.inBetween(cX, l.startX, endX2) && CalcTools.inBetween(cY - 1, l.startY, endY) && (cY - l.startY) * (-1) == ((cX - 1) - l.startX) * lSlope && CalcTools.inBetween(cX - 1, l.startX, endX2) && CalcTools.inBetween(cY, l.startY, endY) && !toCalc.contains(l)) {
                                toCalc.add(l);
                            }
                            if (doorCollider.xLoc + 1 == w.xLoc && doorCollider.yLoc - 1 == w.yLoc && ((cY - 1) - l.startY) * (-1) == (cX - l.startX) * lSlope && CalcTools.inBetween(cX, l.startX, endX2) && CalcTools.inBetween(cY - 1, l.startY, endY) && (cY - l.startY) * (-1) == ((cX + 1) - l.startX) * lSlope && CalcTools.inBetween(cX + 1, l.startX, endX2) && CalcTools.inBetween(cY, l.startY, endY) && !toCalc.contains(l)) {
                                toCalc.add(l);
                            }
                            if (doorCollider.xLoc - 1 == w.xLoc && doorCollider.yLoc + 1 == w.yLoc && ((cY + 1) - l.startY) * (-1) == (cX - l.startX) * lSlope && CalcTools.inBetween(cX, l.startX, endX2) && CalcTools.inBetween(cY + 1, l.startY, endY) && (cY - l.startY) * (-1) == ((cX - 1) - l.startX) * lSlope && CalcTools.inBetween(cX - 1, l.startX, endX2) && CalcTools.inBetween(cY, l.startY, endY) && !toCalc.contains(l)) {
                                toCalc.add(l);
                            }
                        }
                    }
                }
            }
        }
        Iterator<LaserInstance> it3 = toCalc.iterator();
        while (it3.hasNext()) {
            LaserInstance lr = it3.next();
            if (this.LaserArray.contains(lr)) {
                boolean produceOutput = false;
                if (toCalc.size() > 1 && toCalc.lastIndexOf(lr) == 0) {
                    Iterator<Door> it4 = toCalc.get(1).passedDoors.iterator();
                    while (true) {
                        if (!it4.hasNext()) {
                            break;
                        }
                        CollisionObject d2 = (Door) it4.next();
                        this.traceDoorPrisms.clear();
                        if (tracedDoor(d2, lr)) {
                            produceOutput = true;
                            this.traceDoorPrisms.clear();
                            break;
                        }
                    }
                }
                if (lr.child != null && lr.child.type == 2) {
                    Prism PrismObject = (Prism) lr.child;
                    if (!PrismObject.locked) {
                        if ((PrismObject.slotsCheck[0] == 1 && PrismObject.slotsCheck[1] == 1) || ((PrismObject.slotsCheck[0] == 1 && PrismObject.slotsCheck[2] == 1) || (PrismObject.slotsCheck[1] == 1 && PrismObject.slotsCheck[2] == 1))) {
                            int laserToKill = 0;
                            int laserToCalc = 0;
                            boolean doCalc = false;
                            if (PrismObject.slots[0] == lr) {
                                if (PrismObject.slotsCheck[1] == 1) {
                                    laserToKill = 2;
                                    laserToCalc = 1;
                                } else {
                                    laserToKill = 1;
                                    laserToCalc = 2;
                                }
                                doCalc = true;
                            } else if (PrismObject.slots[1] == lr) {
                                if (PrismObject.slotsCheck[0] == 1) {
                                    laserToKill = 2;
                                    laserToCalc = 0;
                                } else {
                                    laserToKill = 0;
                                    laserToCalc = 2;
                                }
                                doCalc = true;
                            } else if (PrismObject.slots[2] == lr) {
                                if (PrismObject.slotsCheck[0] == 1) {
                                    laserToKill = 1;
                                    laserToCalc = 0;
                                } else {
                                    laserToKill = 0;
                                    laserToCalc = 1;
                                }
                                doCalc = true;
                            }
                            LaserInstance tempLaser = PrismObject.slots[laserToCalc];
                            if (doCalc) {
                                killLaserInstance(PrismObject.slots[laserToKill]);
                                for (int x = 0; x < 3; x++) {
                                    PrismObject.slots[x] = null;
                                    PrismObject.slotsCheck[x] = 0;
                                }
                            }
                            if (toCalc.get(0) == lr && killOutput) {
                                if (this.killOutputPrism != null) {
                                    if (PrismObject == this.killOutputPrism) {
                                        laserCalcCollision(tempLaser, false);
                                    } else {
                                        laserCalcCollision(tempLaser, true);
                                    }
                                } else {
                                    laserCalcCollision(tempLaser, false);
                                }
                            } else {
                                laserCalcCollision(tempLaser, true);
                            }
                        } else if (lr == PrismObject.slots[0] || lr == PrismObject.slots[1] || lr == PrismObject.slots[2]) {
                            ArrayList<LaserInstance> lasersToKill = new ArrayList<>();
                            for (int x2 = 0; x2 < 3; x2++) {
                                if (PrismObject.slotsCheck[x2] == 2) {
                                    lasersToKill.add(PrismObject.slots[x2]);
                                }
                                PrismObject.slotsCheck[x2] = 0;
                                PrismObject.slots[x2] = null;
                            }
                            Iterator<LaserInstance> it5 = lasersToKill.iterator();
                            while (it5.hasNext()) {
                                killLaserInstance(it5.next());
                            }
                        }
                    }
                } else if (lr.child != null && lr.child.type == 1) {
                    Mirror MirrorObject = (Mirror) lr.child;
                    if (lr == MirrorObject.laserIn) {
                        killLaserInstance(MirrorObject.laserOut);
                        MirrorObject.laserOut = null;
                        MirrorObject.laserIn = null;
                        MirrorObject.isActive = false;
                    }
                } else if (lr.child != null && lr.child.type == 3) {
                    Checkpoint checkObject = (Checkpoint) lr.child;
                    if (checkObject.inLaser == lr) {
                        checkObject.inLaser = null;
                        checkObject.checked = 0;
                    }
                } else if (lr.child != null && lr.child.type == 4) {
                    Switch switchObject = (Switch) lr.child;
                    if (switchObject.inLaser == lr) {
                        checkSwitchStatus(switchObject);
                    }
                } else if (lr.child != null && lr.child.type == 6) {
                    Portal portalObject = (Portal) lr.child;
                    Iterator<CollisionObject> it6 = this.PortalArray.iterator();
                    while (it6.hasNext()) {
                        CollisionObject p = it6.next();
                        if (p != portalObject) {
                            Portal outputPortal = (Portal) p;
                            int laserIndex = ((int) ((lr.angle + 180.0d) % 360.0d)) / 45;
                            killLaserInstance(outputPortal.laser[laserIndex]);
                            outputPortal.laser[laserIndex] = null;
                            portalObject.laser[((int) (lr.angle % 360.0d)) / 45] = null;
                        }
                    }
                }
                lr.purgeCurPassedDoors();
                if (this.LaserArray.contains(lr)) {
                    if (toCalc.size() > 1 && toCalc.lastIndexOf(lr) == 0) {
                        laserCalcCollision(lr, produceOutput);
                    } else {
                        laserCalcCollision(lr, true);
                    }
                }
            }
        }
    }

    public void placeTemp(int x, int y) {
        if (this.tempHolder != null) {
            if (x != this.tempHolder.xLoc || y != this.tempHolder.yLoc) {
                if (this.tempHolder.type == 2) {
                    this.tempHolder = new Prism(0, 0, 0.0d);
                }
                this.laserToAngle = null;
            }
            if (this.tempHolder.type == 2) {
                this.curPrism++;
                this.PrismArray.add(this.tempHolder);
                this.orderPlaceArray.add(this.tempHolder);
                if (false && false) {
                    
                }
            } else if (this.tempHolder.type == 1) {
                this.curMirror++;
                this.MirrorArray.add(this.tempHolder);
                this.orderPlaceArray.add(this.tempHolder);
                if (false && false) {
                    
                }
            }
            if (x > GRID_X_DIM) {
                x = GRID_X_DIM;
            }
            if (y > GRID_Y_DIM) {
                x = GRID_Y_DIM;
            }
            if (y < 1) {
                y = 1;
            }
            if (x < 1) {
                x = 1;
            }
            this.tempHolder.xLoc = x;
            this.tempHolder.yLoc = y;
            this.tempObjectLoc[0] = -100;
            this.tempObjectLoc[1] = -100;
            this.ObjectArray.add(this.tempHolder);
            laserSearch(this.tempHolder, false);
            this.tempSelected = this.tempHolder;
            calcLaserToAngle();
            this.tempHolder = null;
        }
    }

    private void calcLaserToAngle() {
        if (this.tempSelected != null && this.tempSelected.type == 2) {
            Prism tempPrism = (Prism) this.tempSelected;
            for (int x = 0; x < 3; x++) {
                if (tempPrism.slotsCheck[x] == 2) {
                    this.laserToAngle = tempPrism.slots[x];
                    return;
                }
            }
        }
    }

    private void rotateOutput(int direction) {
        if (this.tempSelected.type == 1 && this.canRotateAngle) {
            Mirror tempMirror = (Mirror) this.tempSelected;
            int xLoc = tempMirror.xLoc;
            int yLoc = tempMirror.yLoc;
            double oldAngle = tempMirror.angle;
            killCollObject(tempMirror);
            this.tempSelected = newMirror(xLoc, yLoc, CalcTools.angleCorrect(oldAngle + (22.5d * direction)));
            this.curMirror++;
            if (false) {
                
                return;
            }
            return;
        }
        if (this.tempSelected.type == 2 && this.laserToAngle != null && this.canRotateAngle) {
            double nextStep = direction * 45;
            Prism tempPrism = (Prism) this.tempSelected;
            int xLoc2 = tempPrism.xLoc;
            int yLoc2 = tempPrism.yLoc;
            int color = this.laserToAngle.getColor();
            double oldAngle2 = this.laserToAngle.angle;
            ArrayList<Prism> prisms = (ArrayList) this.laserToAngle.parentPrisms.clone();
            ArrayList<Door> doors = (ArrayList) this.laserToAngle.passedDoors.clone();
            doors.removeAll(this.laserToAngle.induvidualPassedDoors);
            double newAngle = CalcTools.angleCorrect(oldAngle2 + nextStep);
            LaserInstance laserToKill = this.laserToAngle;
            int slot = 0;
            int otherSlot = 0;
            int outputCount = 0;
            for (int i = 0; i < 3; i++) {
                if (tempPrism.slotsCheck[i] == 2) {
                    if (tempPrism.slots[i] == this.laserToAngle) {
                        slot = i;
                    } else {
                        otherSlot = i;
                    }
                    outputCount++;
                }
            }
            boolean notValidAngle = true;
            while (notValidAngle) {
                boolean conflict = false;
                for (int x = 0; x < 3; x++) {
                    if (tempPrism.slotsCheck[x] == 1) {
                        if (tempPrism.slots[x].angle == CalcTools.angleCorrect(180.0d + newAngle)) {
                            conflict = true;
                        }
                    } else if (tempPrism.slotsCheck[x] == 2 && tempPrism.slots[x].angle == newAngle) {
                        conflict = true;
                    }
                }
                if (conflict) {
                    newAngle = CalcTools.angleCorrect(newAngle + nextStep);
                } else {
                    notValidAngle = false;
                }
            }
            if (outputCount == 1) {
                tempPrism.storedSingleAngle = (int) newAngle;
            } else if (outputCount == 2) {
                if (tempPrism.storedAngles[1][0] == 361 || (oldAngle2 == tempPrism.storedAngles[1][0] && color == tempPrism.storedAngles[1][1])) {
                    tempPrism.storedAngles[1][0] = (int) newAngle;
                    tempPrism.storedAngles[1][1] = color;
                    tempPrism.storedAngles[2][0] = (int) tempPrism.slots[otherSlot].angle;
                    tempPrism.storedAngles[2][1] = tempPrism.slots[otherSlot].getColor();
                } else if (tempPrism.storedAngles[2][0] == 361 || (oldAngle2 == tempPrism.storedAngles[2][0] && color == tempPrism.storedAngles[2][1])) {
                    tempPrism.storedAngles[2][0] = (int) newAngle;
                    tempPrism.storedAngles[2][1] = color;
                    tempPrism.storedAngles[1][0] = (int) tempPrism.slots[otherSlot].angle;
                    tempPrism.storedAngles[1][1] = tempPrism.slots[otherSlot].getColor();
                }
            }
            LaserInstance tempLaser = new LaserInstance(xLoc2, yLoc2, newAngle, color, prisms, doors);
            this.LaserArray.add(tempLaser);
            tempPrism.slots[slot] = tempLaser;
            laserCalcCollision(tempLaser, true);
            this.laserToAngle = tempPrism.slots[slot];
            this.previousLaserToAngle = this.laserToAngle;
            killLaserInstance(laserToKill);
            if (false) {
                
            }
        }
    }

    private int laserDistance() {
        double distanceD = 0.0d;
        Iterator<LaserInstance> it = this.LaserArray.iterator();
        while (it.hasNext()) {
            LaserInstance l = it.next();
            distanceD += Math.sqrt(Math.pow(l.startX - l.endX, 2.0d) + Math.pow(l.startY - l.endY, 2.0d));
        }
        int distanceI = (int) Math.round(distanceD);
        return distanceI;
    }

    public void checkSwitchStatus(CollisionObject object) {
        boolean turnSwitchOff = true;
        Switch switchObject = (Switch) object;
        if (switchObject.isUnlocked()) {
            Iterator<LaserInstance> it = this.LaserArray.iterator();
            while (true) {
                if (!it.hasNext()) {
                    break;
                }
                LaserInstance laser = it.next();
                if (laser.child == switchObject && laser.getColor() == switchObject.getColor()) {
                    toggleSwitchOn(switchObject, laser);
                    turnSwitchOff = false;
                    break;
                }
            }
            if (turnSwitchOff) {
                toggleSwitchOff(switchObject);
            }
        }
    }

    private void toggleSwitchOn(Switch switchObject, LaserInstance laser) {
        if (switchObject.isUnlocked()) {
            switchObject.toggleOn(laser);
            openDoorsOfColor(switchObject.getColor());
        }
    }

    private void toggleSwitchOn(Switch switchObject) {
        if (switchObject.isUnlocked()) {
            switchObject.toggleOn();
            openDoorsOfColor(switchObject.getColor());
        }
    }

    private void toggleSwitchOff(Switch switchObject) {
        if (switchObject.isUnlocked()) {
            switchObject.toggleOff();
            closeDoorsOfColor(switchObject.getColor());
        }
    }

    private void openDoorsOfColor(int color) {
        Iterator<CollisionObject> it = this.DoorArray.iterator();
        while (it.hasNext()) {
            CollisionObject object = it.next();
            Door door = (Door) object;
            if (door.getColor() == color) {
                toggleDoorOpen(door);
            }
        }
    }

    private void closeDoorsOfColor(int color) {
        Iterator<CollisionObject> it = this.DoorArray.iterator();
        while (it.hasNext()) {
            CollisionObject object = it.next();
            Door door = (Door) object;
            if (door.getColor() == color) {
                toggleDoorClosed(door);
            }
        }
    }

    private void toggleDoorOpen(Door door) {
        if (door.isUnlocked()) {
            this.ObjectArray.remove(door);
            door.setOpenTo(true);
            if (door.inLaser != null) {
                laserCalcCollision(door.inLaser, false);
            }
        }
    }

    private void toggleDoorClosed(Door door) {
        if (door.isUnlocked()) {
            CollisionObject tempObject = new Dummy();
            boolean killTempObject = false;
            this.ObjectArray.add(door);
            door.setOpenTo(false);
            Iterator<CollisionObject> it = this.PrismArray.iterator();
            while (true) {
                if (!it.hasNext()) {
                    break;
                }
                CollisionObject prism = it.next();
                if (prism.xLoc == door.xLoc && prism.yLoc == door.yLoc) {
                    tempObject = prism;
                    killTempObject = true;
                    break;
                }
            }
            if (!killTempObject) {
                Iterator<CollisionObject> it2 = this.MirrorArray.iterator();
                while (true) {
                    if (!it2.hasNext()) {
                        break;
                    }
                    CollisionObject mirror = it2.next();
                    if (mirror.xLoc == door.xLoc && mirror.yLoc == door.yLoc) {
                        tempObject = mirror;
                        killTempObject = true;
                        break;
                    }
                }
            }
            if (killTempObject) {
                killCollObject(tempObject);
                if (this.tempSelected == tempObject) {
                    this.tempSelected = null;
                }
            }
            laserSearch(door, false);
        }
    }

    private void activateReactor(Emitter reactor, int color) {
        reactor.color = color;
        LaserInstance reactorLaser = new LaserInstance(reactor.xLoc, reactor.yLoc, reactor.angle, reactor.color, new ArrayList(), new ArrayList());
        this.LaserArray.add(reactorLaser);
        laserCalcCollision(reactorLaser, true);
    }
    // ---------------- harness helpers ----------------
    public void loadStatic(String levelData) {
        String[] levelArray = levelData.trim().split("\\;");
        String[] head = levelArray[0].split("\\,");
        maxMirror = Integer.parseInt(head[0]); maxPrism = Integer.parseInt(head[1]);
        for (int i = 1; i < levelArray.length; i++) {
            String[] f = levelArray[i].split("\\,");
            int t; try { t = Integer.parseInt(f[2]); } catch (NumberFormatException e) { t = 0; }
            int x = Integer.parseInt(f[0]), y = Integer.parseInt(f[1]);
            if (t == 8) newWall(x, y);
            else if (t == 7) newEmitter(x, y, Integer.parseInt(f[4]), Double.valueOf(f[3]));
            else if (t == 3) newCheckpoint(x, y, Integer.parseInt(f[4]));
            else if (t == 4) newSwitch(x, y, Integer.parseInt(f[4]));
            else if (t == 5) newDoor(x, y, Integer.parseInt(f[4]), false);
            else if (t == 6) newPortal(x, y);
        }
        for (CollisionObject o : SwitchArray) { Switch s = (Switch) o; toggleSwitchOn(s); s.setLockedTo(true); }
    }
    public void fireEmitters() {
        for (CollisionObject o : EmitterArray) {
            Emitter e5 = (Emitter) o;
            if (e5.color != 0) {
                LaserInstance l = new LaserInstance(e5.xLoc, e5.yLoc, e5.angle, e5.color, new ArrayList(), new ArrayList());
                LaserArray.add(l); laserCalcCollision(l, true); e5.setChildLaser(l);
            }
        }
        for (CollisionObject o : SwitchArray) { ((Switch) o).setLockedTo(false); checkSwitchStatus(o); }
    }
    // place a solution piece BEFORE emitters fire (like loading a saved solution)
    public void preplaceMirror(int x, int y, double a) { newMirror(x, y, a); curMirror++; }
    public void preplacePrism(int x, int y, int[][] stored, int single) {
        Prism p = new Prism(x, y, 0.0d);
        if (stored != null) { p.storedAngles[1][0] = stored[0][0]; p.storedAngles[1][1] = stored[0][1]; p.storedAngles[2][0] = stored[1][0]; p.storedAngles[2][1] = stored[1][1]; }
        if (single != 360) { p.storedSingleAngle = single; this.killOutputPrism = p; }
        PrismArray.add(p); ObjectArray.add(p); orderPlaceArray.add(p);
        laserSearch(p, single != 360); curPrism++;
    }
    public void gc() { garbageCollect(); }
    public boolean placeMirror(int x, int y) { if (gridOccupied(x, y)) return false; tempHolder = new Mirror(0, 0, 135.0d); placeTemp(x, y); return true; }
    public boolean placePrism(int x, int y) { if (gridOccupied(x, y)) return false; tempHolder = new Prism(0, 0, 0.0d); placeTemp(x, y); return true; }
    public void select(CollisionObject o) { tempSelected = o; laserToAngle = null; calcLaserToAngle(); }
    public void rotateSelected(int dir) { if (tempSelected != null) rotateOutput(dir); }
    public void selectPrismOutput(int k) {
        if (tempSelected == null || tempSelected.type != 2) return;
        Prism p = (Prism) tempSelected; int n = 0;
        for (int i = 0; i < 3; i++) if (p.slotsCheck[i] == 2) { if (n == k) laserToAngle = p.slots[i]; n++; }
    }
    public void delete(CollisionObject o) { killCollObject(o); if (tempSelected == o) tempSelected = null; }
    public boolean checkpointsChecked() { if (CheckpointArray.isEmpty()) return false; for (CollisionObject c : CheckpointArray) if (((Checkpoint) c).checked == 0) return false; return true; }
}
