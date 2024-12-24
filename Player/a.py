import math

lod = 2

class Vector2:
    def __init__(self, x=0.0, y=0.0):
        self.x = x
        self.y = y

    def __repr__(self):
        return f"({self.x}, {self.y})"
    
    def offset(self, x_offset = 0.0, y_offset = 0.0):
        return Vector2(self.x + x_offset, self.y + y_offset)

def getSquare(origin:Vector2, max_depth:float, scale:float=1):
   
    if scale == max_depth:
        return [
            origin.offset(scale, 0),
            origin.offset(-scale, 0),
            origin.offset(0, scale),
            origin.offset(0, -scale)
        ]
    
    scale /= 2

    a : Vector2 = origin.offset(scale, scale)
    b : Vector2 = origin.offset(-scale, scale)
    c : Vector2 = origin.offset(-scale, -scale)
    d : Vector2 = origin.offset(scale, -scale)

    return getSquare(a, max_depth, scale) + \
           getSquare(b, max_depth, scale) + \
           getSquare(c, max_depth, scale) + \
           getSquare(d, max_depth, scale)


print(getSquare(Vector2(), 1/(pow(2, lod))))
