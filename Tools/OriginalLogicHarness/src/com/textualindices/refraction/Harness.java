package com.textualindices.refraction;

import java.nio.file.*;
import java.util.*;

/** Plays random move sequences on the ORIGINAL game logic and prints the resulting state as JSON lines. */
public class Harness {
    static String laserJson(OrigSim s) {
        StringBuilder sb = new StringBuilder("[");
        boolean first = true;
        for (LaserInstance l : s.LaserArray) {
            if (!first) sb.append(',');
            first = false;
            CollisionObject c = l.child;
            String hit = "none"; int hx = 0, hy = 0;
            if (c != null && !(c instanceof Dummy)) {
                hx = c.xLoc; hy = c.yLoc;
                if (c instanceof Wall) hit = "wall";
                else if (c instanceof Mirror) hit = "mirror";
                else if (c instanceof Prism) hit = "prism";
                else if (c instanceof Door) hit = "door";
                else if (c instanceof Checkpoint) hit = "target";
                else if (c instanceof Switch) hit = "switch";
                else if (c instanceof Portal) hit = "portal";
                else if (c instanceof Emitter) hit = "emitter";
                else hit = "obj" + c.type;
            }
            sb.append(String.format(Locale.ROOT, "[%d,%d,%d,%d,\"%s\",%d,%d]", l.startX, l.startY, (int) Math.round(((l.angle % 360) + 360) % 360), l.getColor(), hit, hx, hy));
        }
        return sb.append(']').toString();
    }

    static String cpJson(OrigSim s) {
        StringBuilder sb = new StringBuilder("[");
        for (int i = 0; i < s.CheckpointArray.size(); i++) {
            if (i > 0) sb.append(',');
            sb.append(((Checkpoint) s.CheckpointArray.get(i)).checked);
        }
        return sb.append(']').toString();
    }

    static String piecesJson(OrigSim s) {
        StringBuilder sb = new StringBuilder("[");
        boolean first = true;
        for (CollisionObject c : s.orderPlaceArray) {
            if (!first) sb.append(',');
            first = false;
            if (c.type == 1) sb.append(String.format(Locale.ROOT, "{\"type\":\"mirror\",\"x\":%d,\"y\":%d,\"angle\":%s}", c.xLoc, c.yLoc, Double.toString(((c.angle % 360) + 360) % 360)));
            else {
                Prism p = (Prism) c;
                List<LaserInstance> outs = new ArrayList<>();
                for (int i = 0; i < 3; i++) if (p.slotsCheck[i] == 2 && p.slots[i] != null) outs.add(p.slots[i]);
                sb.append(String.format(Locale.ROOT, "{\"type\":\"prism\",\"x\":%d,\"y\":%d", c.xLoc, c.yLoc));
                if (outs.size() == 2) {
                    sb.append(String.format(Locale.ROOT, ",\"split\":[{\"color\":%d,\"dir\":%d},{\"color\":%d,\"dir\":%d}]",
                        outs.get(0).getColor(), (int) outs.get(0).angle, outs.get(1).getColor(), (int) outs.get(1).angle));
                } else if (outs.size() == 1) {
                    sb.append(String.format(Locale.ROOT, ",\"combineDir\":%d", (int) outs.get(0).angle));
                }
                sb.append('}');
            }
        }
        return sb.append(']').toString();
    }

    static List<int[]> laserCells(OrigSim s) {
        List<int[]> cells = new ArrayList<>();
        for (LaserInstance l : s.LaserArray) {
            int dx, dy;
            int a = (int) Math.round(((l.angle % 360) + 360) % 360);
            switch (a) { case 0: dx=1;dy=0;break; case 45: dx=1;dy=-1;break; case 90: dx=0;dy=-1;break; case 135: dx=-1;dy=-1;break;
                case 180: dx=-1;dy=0;break; case 225: dx=-1;dy=1;break; case 270: dx=0;dy=1;break; default: dx=1;dy=1; }
            int x = l.startX, y = l.startY;
            for (int k = 0; k < 12; k++) {
                x += dx; y += dy;
                if (x < 1 || y < 1 || x > 8 || y > 10) break;
                if (!s.gridOccupied(x, y)) cells.add(new int[]{x, y});
                if (x == l.endX && y == l.endY) break;
            }
        }
        return cells;
    }

    public static void main(String[] args) throws Exception {
        String levelsDir = args[0];
        int firstSeed = Integer.parseInt(args[1]);
        int runs = Integer.parseInt(args[2]);
        for (int run = 0; run < runs; run++) {
            int seed = firstSeed + run;
            Random rnd = new Random(seed);
            int lvl = 1 + rnd.nextInt(140);
            String data = new String(Files.readAllBytes(Paths.get(levelsDir, "level" + lvl)));
            OrigSim a = new OrigSim();
            String err = null;
            try {
                a.loadStatic(data);
                a.fireEmitters();
                a.gc();
                int actions = 1 + rnd.nextInt(Integer.getInteger("maxActions", 8));
                for (int k = 0; k < actions; k++) {
                    int what = rnd.nextInt(10);
                    List<int[]> cells = laserCells(a);
                    int[] cell = cells.isEmpty() || rnd.nextInt(5) == 0
                        ? new int[]{1 + rnd.nextInt(8), 1 + rnd.nextInt(10)} : cells.get(rnd.nextInt(cells.size()));
                    if (what < 4) {
                        if (a.placeMirror(cell[0], cell[1])) { int r = rnd.nextInt(16); for (int i = 0; i < r; i++) { a.rotateSelected(1); a.gc(); } }
                    } else if (what < 7) {
                        if (a.placePrism(cell[0], cell[1])) {
                            a.gc();
                            int r = rnd.nextInt(4);
                            for (int i = 0; i < r; i++) { a.selectPrismOutput(rnd.nextInt(2)); a.rotateSelected(rnd.nextBoolean() ? 1 : -1); a.gc(); }
                        }
                    } else if (what < 9 && !a.orderPlaceArray.isEmpty()) {
                        CollisionObject o = a.orderPlaceArray.get(rnd.nextInt(a.orderPlaceArray.size()));
                        a.select(o);
                        if (o.type == 2) a.selectPrismOutput(rnd.nextInt(2));
                        a.rotateSelected(rnd.nextBoolean() ? 1 : -1);
                    } else if (!a.orderPlaceArray.isEmpty()) {
                        a.delete(a.orderPlaceArray.get(rnd.nextInt(a.orderPlaceArray.size())));
                    }
                    a.gc();
                }
            } catch (Throwable t) { err = t.getClass().getSimpleName(); }

            String pieces = piecesJson(a);
            // fresh reload of the same pieces (what the original does when you reopen a saved solution)
            OrigSim b = new OrigSim();
            String errB = null;
            try {
                b.loadStatic(data);
                for (CollisionObject c : a.orderPlaceArray) {
                    if (c.type == 1) b.preplaceMirror(c.xLoc, c.yLoc, c.angle);
                    else {
                        Prism p = (Prism) c;
                        List<LaserInstance> outs = new ArrayList<>();
                        for (int i = 0; i < 3; i++) if (p.slotsCheck[i] == 2 && p.slots[i] != null) outs.add(p.slots[i]);
                        if (outs.size() == 2) b.preplacePrism(c.xLoc, c.yLoc, new int[][]{{(int) outs.get(0).angle, outs.get(0).getColor()}, {(int) outs.get(1).angle, outs.get(1).getColor()}}, 360);
                        else if (outs.size() == 1) b.preplacePrism(c.xLoc, c.yLoc, null, (int) outs.get(0).angle);
                        else b.preplacePrism(c.xLoc, c.yLoc, null, 360);
                    }
                }
                b.fireEmitters();
                b.gc();
            } catch (Throwable t) { errB = t.getClass().getSimpleName(); }

            System.out.println(String.format(Locale.ROOT,
                "{\"seed\":%d,\"level\":%d,\"err\":%s,\"errB\":%s,\"pieces\":%s,\"incLasers\":%s,\"incCp\":%s,\"incWon\":%b,\"freshLasers\":%s,\"freshCp\":%s,\"freshWon\":%b}",
                seed, lvl, err == null ? "null" : "\"" + err + "\"", errB == null ? "null" : "\"" + errB + "\"", pieces,
                laserJson(a), cpJson(a), a.checkpointsChecked(), laserJson(b), cpJson(b), b.checkpointsChecked()));
        }
    }
}
