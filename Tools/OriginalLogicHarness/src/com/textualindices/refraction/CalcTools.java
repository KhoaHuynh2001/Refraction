package com.textualindices.refraction;

import android.graphics.Bitmap;
import android.graphics.Matrix;

/* loaded from: /tmp/claude-0/work/apk/classes.dex */
public class CalcTools {

    /* loaded from: /tmp/claude-0/work/apk/classes.dex */
    public enum MirrorImage {
        HORIZONTAL,
        VERTICAL
    }

    public static double angleCorrect(double rawAngle) {
        if (rawAngle > 315.0d) {
            double modAngle = rawAngle - 360.0d;
            return modAngle;
        }
        if (rawAngle < 0.0d) {
            double modAngle2 = rawAngle + 360.0d;
            return modAngle2;
        }
        return rawAngle;
    }

    public static double combineAngles(double angle1, double angle2) {
        double finalAngle;
        double finalAngle2;
        if (angle1 > angle2) {
            double diffAngle = Math.abs(angle1 - angle2);
            double angleToAdd = diffAngle;
            if (diffAngle > 180.0d) {
                angleToAdd = diffAngle - 180.0d;
            }
            double angleToAdd2 = angleToAdd / 2.0d;
            if (diffAngle >= 180.0d) {
                finalAngle2 = angle1 + angleToAdd2;
            } else {
                finalAngle2 = angle2 + angleToAdd2;
            }
            return angleRound(angleCorrect(finalAngle2), 45.0d);
        }
        double diffAngle2 = Math.abs(angle1 - angle2);
        double angleToAdd3 = diffAngle2;
        if (diffAngle2 > 180.0d) {
            angleToAdd3 = diffAngle2 - 180.0d;
        }
        double angleToAdd4 = angleToAdd3 / 2.0d;
        if (diffAngle2 >= 180.0d) {
            finalAngle = angle2 + angleToAdd4;
        } else {
            finalAngle = angle1 + angleToAdd4;
        }
        return angleRound(angleCorrect(finalAngle), 45.0d);
    }

    private static double angleRound(double angleCorrect, double i) {
        return Math.floor(angleCorrect / i) * i;
    }

    public static boolean inBetween(int a, int x, int y) {
        return x > y ? a >= y && a <= x : y > x ? a >= x && a <= y : a == x;
    }

    public static int[] splitColor(int color) {
        int[] colorOut = new int[2];
        if (color < 4) {
            colorOut[0] = color;
            colorOut[1] = color;
        } else if (color == 5) {
            colorOut[0] = 2;
            colorOut[1] = 3;
        } else if (color == 4) {
            colorOut[0] = 2;
            colorOut[1] = 1;
        } else if (color == 6) {
            colorOut[0] = 1;
            colorOut[1] = 3;
        } else {
            colorOut[0] = color;
            colorOut[1] = color;
        }
        return colorOut;
    }

    public static int combineColor(int[] colorIn) {
        if (colorIn[0] == colorIn[1]) {
            int colorComb = colorIn[1];
            return colorComb;
        }
        if ((colorIn[0] == 3 && colorIn[1] == 2) || (colorIn[1] == 3 && colorIn[0] == 2)) {
            return 5;
        }
        if ((colorIn[0] == 1 && colorIn[1] == 2) || (colorIn[1] == 1 && colorIn[0] == 2)) {
            return 4;
        }
        if ((colorIn[0] != 3 || colorIn[1] != 1) && (colorIn[1] != 3 || colorIn[0] != 1)) {
            return 0;
        }
        return 6;
    }

    public static Bitmap scaleBitmap(float ratio, Bitmap image) {
        int width = Math.round(image.getWidth() * ratio);
        int height = Math.round(image.getHeight() * ratio);
        if (width == 0) {
            width = 1;
        }
        if (height == 0) {
            height = 1;
        }
        try {
            return Bitmap.createScaledBitmap(image, width, height, true);
        } catch (IllegalArgumentException e) {
            e.printStackTrace();
            return image;
        }
    }

    public static Bitmap scaleBitmapHeight(float ratio, Bitmap image) {
        int height = Math.round(image.getHeight() * ratio);
        if (height == 0) {
            height = 1;
        }
        try {
            return Bitmap.createScaledBitmap(image, image.getWidth(), height, true);
        } catch (IllegalArgumentException e) {
            e.printStackTrace();
            return image;
        }
    }

    public static Bitmap rotateBitmap(int rotation, Bitmap image) {
        int width = image.getWidth();
        int height = image.getHeight();
        Matrix matrix = new Matrix();
        matrix.postRotate(rotation);
        try {
            return Bitmap.createBitmap(image, 0, 0, width, height, matrix, true);
        } catch (IllegalArgumentException e) {
            e.printStackTrace();
            return image;
        }
    }

    public static Bitmap mirrorBitmap(MirrorImage mirrorDirection, Bitmap image) {
        int width = image.getWidth();
        int height = image.getHeight();
        int widthCenter = width / 2;
        int heightCenter = height / 2;
        Matrix matrix = new Matrix();
        switch (mirrorDirection) {
            case HORIZONTAL:
                matrix.postScale(-1.0f, 1.0f, widthCenter, heightCenter);
                break;
            case VERTICAL:
                matrix.postScale(1.0f, -1.0f, widthCenter, heightCenter);
                break;
        }
        try {
            return Bitmap.createBitmap(image, 0, 0, width, height, matrix, true);
        } catch (IllegalArgumentException e) {
            e.printStackTrace();
            return image;
        }
    }

    public static int returnSmallerNumber(int num1, int num2) {
        return num1 > num2 ? num2 : num1;
    }

    public static String colorMap(int colorCode) {
        if (colorCode == 1) {
            return "Blue";
        }
        if (colorCode == 2) {
            return "Yellow";
        }
        if (colorCode == 3) {
            return "Red";
        }
        if (colorCode == 4) {
            return "Green";
        }
        if (colorCode == 5) {
            return "Orange";
        }
        if (colorCode == 6) {
            return "Purple";
        }
        return "No Color";
    }

    public static double[] prismOutputAngle(LaserInstance laser) {
        double[] outputAngle = new double[2];
        double angle = laser.angle;
        int startX = laser.endX;
        int startY = laser.endY;
        if (startY == 1) {
            if (startX == 1) {
                if (angle == 90.0d) {
                    outputAngle[0] = 315.0d;
                    outputAngle[1] = 0.0d;
                } else if (angle == 135.0d) {
                    outputAngle[0] = 270.0d;
                    outputAngle[1] = 0.0d;
                } else {
                    outputAngle[0] = 270.0d;
                    outputAngle[1] = 315.0d;
                }
            } else if (startX == 8) {
                if (angle == 90.0d) {
                    outputAngle[0] = 180.0d;
                    outputAngle[1] = 225.0d;
                } else if (angle == 45.0d) {
                    outputAngle[0] = 180.0d;
                    outputAngle[1] = 270.0d;
                } else {
                    outputAngle[0] = 225.0d;
                    outputAngle[1] = 270.0d;
                }
            } else if (angle == 0.0d) {
                outputAngle[0] = 0.0d;
                outputAngle[1] = 315.0d;
            } else if (angle == 180.0d) {
                outputAngle[0] = 180.0d;
                outputAngle[1] = 225.0d;
            } else {
                outputAngle[0] = 0.0d;
                outputAngle[1] = 180.0d;
            }
        } else if (startY == 10) {
            if (startX == 1) {
                if (angle == 270.0d) {
                    outputAngle[0] = 45.0d;
                    outputAngle[1] = 0.0d;
                } else if (angle == 225.0d) {
                    outputAngle[0] = 90.0d;
                    outputAngle[1] = 0.0d;
                } else {
                    outputAngle[0] = 90.0d;
                    outputAngle[1] = 45.0d;
                }
            } else if (startX == 8) {
                if (angle == 270.0d) {
                    outputAngle[0] = 135.0d;
                    outputAngle[1] = 180.0d;
                } else if (angle == 315.0d) {
                    outputAngle[0] = 90.0d;
                    outputAngle[1] = 180.0d;
                } else {
                    outputAngle[0] = 90.0d;
                    outputAngle[1] = 135.0d;
                }
            } else if (angle == 0.0d) {
                outputAngle[0] = 0.0d;
                outputAngle[1] = 45.0d;
            } else if (angle == 180.0d) {
                outputAngle[0] = 180.0d;
                outputAngle[1] = 135.0d;
            } else {
                outputAngle[0] = 0.0d;
                outputAngle[1] = 180.0d;
            }
        } else if (startX == 1) {
            if (angle == 270.0d) {
                outputAngle[0] = 270.0d;
                outputAngle[1] = 315.0d;
            } else if (angle == 90.0d) {
                outputAngle[0] = 90.0d;
                outputAngle[1] = 45.0d;
            } else {
                outputAngle[0] = 90.0d;
                outputAngle[1] = 270.0d;
            }
        } else if (startX != 8) {
            outputAngle[0] = angleCorrect(45.0d + angle);
            outputAngle[1] = angleCorrect(angle - 45.0d);
        } else if (angle == 270.0d) {
            outputAngle[0] = 270.0d;
            outputAngle[1] = 225.0d;
        } else if (angle == 90.0d) {
            outputAngle[0] = 90.0d;
            outputAngle[1] = 135.0d;
        } else {
            outputAngle[0] = 90.0d;
            outputAngle[1] = 270.0d;
        }
        return outputAngle;
    }

    public static double prismOutputAngle(LaserInstance laser1, double angle2) {
        double angle1 = laser1.angle;
        int startX = laser1.endX;
        int startY = laser1.endY;
        if (startY == 1) {
            if (startX == 1) {
                if ((angle1 == 90.0d && angle2 == 135.0d) || (angle1 == 135.0d && angle2 == 90.0d)) {
                    return 0.0d;
                }
                if ((angle1 == 90.0d && angle2 == 180.0d) || (angle1 == 180.0d && angle2 == 90.0d)) {
                    return 315.0d;
                }
                return 270.0d;
            }
            if (startX == 8) {
                if ((angle1 == 90.0d && angle2 == 45.0d) || (angle1 == 45.0d && angle2 == 90.0d)) {
                    return 180.0d;
                }
                if ((angle1 == 90.0d && angle2 == 0.0d) || (angle1 == 0.0d && angle2 == 90.0d)) {
                    return 225.0d;
                }
                return 270.0d;
            }
            double outputAngle = combineAngles(angle1, angle2);
            if (outputAngle >= 90.0d && outputAngle <= 180.0d) {
                if (angle1 == 0.0d || angle2 == 0.0d) {
                    return 225.0d;
                }
                return 180.0d;
            }
            if (outputAngle != 45.0d && outputAngle != 0.0d) {
                return outputAngle;
            }
            if (angle1 == 180.0d || angle2 == 180.0d) {
                return 315.0d;
            }
            return 0.0d;
        }
        if (startY == 10) {
            if (startX == 1) {
                if ((angle1 == 270.0d && angle2 == 225.0d) || (angle1 == 225.0d && angle2 == 270.0d)) {
                    return 0.0d;
                }
                if ((angle1 == 270.0d && angle2 == 180.0d) || (angle1 == 180.0d && angle2 == 270.0d)) {
                    return 45.0d;
                }
                return 90.0d;
            }
            if (startX == 8) {
                if ((angle1 == 270.0d && angle2 == 315.0d) || (angle1 == 315.0d && angle2 == 270.0d)) {
                    return 180.0d;
                }
                if ((angle1 == 270.0d && angle2 == 0.0d) || (angle1 == 0.0d && angle2 == 270.0d)) {
                    return 135.0d;
                }
                return 90.0d;
            }
            double outputAngle2 = combineAngles(angle1, angle2);
            if (outputAngle2 >= 180.0d && outputAngle2 <= 270.0d) {
                if (angle1 == 0.0d || angle2 == 0.0d) {
                    return 135.0d;
                }
                return 180.0d;
            }
            if (outputAngle2 != 315.0d && outputAngle2 != 0.0d) {
                return outputAngle2;
            }
            if (angle1 == 180.0d || angle2 == 180.0d) {
                return 45.0d;
            }
            return 0.0d;
        }
        if (startX == 1) {
            double outputAngle3 = combineAngles(angle1, angle2);
            if (outputAngle3 >= 90.0d && outputAngle3 <= 180.0d) {
                if (angle1 == 270.0d || angle2 == 270.0d) {
                    return 45.0d;
                }
                return 90.0d;
            }
            if (outputAngle3 != 225.0d && outputAngle3 != 270.0d) {
                return outputAngle3;
            }
            if (angle1 == 90.0d || angle2 == 90.0d) {
                return 315.0d;
            }
            return 270.0d;
        }
        if (startX == 8) {
            double outputAngle4 = combineAngles(angle1, angle2);
            if (outputAngle4 >= 0.0d && outputAngle4 <= 90.0d) {
                if (angle1 == 270.0d || angle2 == 270.0d) {
                    return 135.0d;
                }
                return 90.0d;
            }
            if (outputAngle4 != 315.0d && outputAngle4 != 270.0d) {
                return outputAngle4;
            }
            if (angle1 == 90.0d || angle2 == 90.0d) {
                return 225.0d;
            }
            return 270.0d;
        }
        return combineAngles(angle1, angle2);
    }

    /* loaded from: /tmp/claude-0/work/apk/classes.dex */
    public class Orientation {
        public static final int LANDSCAPE_LEFT = 90;
        public static final int LANDSCAPE_RIGHT = 270;
        public static final int PORTRAIT_UPRIGHT = 0;
        public static final int PORTRAIT_UPSIDEDOWN = 180;

        public Orientation() {
        }
    }
}
