def summerize_from(start, end):
    res = 0
    for i in range(start, end + 1):
        res += i
    return res

def get_rotation(b1b2, b1, b2):
    a = (b1b2 ^ 0x2)
    b = (a | 0x1)
    c = (b1 ^ b2)

    return b * c

def get_translation(b1):
    translation = [None, None]
    translation[0] = b1 & 0b1 
    translation[1] = b1 ^ 0b1
    translation[0] * 0.5
    translation[1] * 0.5
    return translation


def quick_cos(b1b2):
    b1b2 &= 3
    b1 = b1b2 >> 1
    b2 = b1b2 & 1
    bn2 = b2 ^ 1
    return (bn2 - 2 * (b1 & bn2))


    # return ~b2 - (2 * (b1 & ~b2))

def quick_sin(b1b2):
    b1b2 &= 3
    b1 = b1b2 >> 1
    b2 = b1b2 & 1

    return (b2 - 2 * (b1 & b2))


def rotate(rotation_index, translation):
    r = [0, 0]
    cosT = quick_cos(rotation_index)
    sinT = quick_sin(rotation_index)

    r[0] = cosT * translation[0] - sinT * translation[1]
    r[1] = sinT * translation[0] + cosT * translation[1]
    return r

def get_branching(key, level, msb):
  
    mask = (0b11 << (msb % 32)) >> (level * 2)

    domain_index = (msb % 32) // 2
  

    if msb >= 32:
        if domain_index < level:

            mask = (0b11 << 30) >> ((level - 1 - domain_index) * 2)
            return (key[1] & mask) >> (msb - (2 * level))
        
        return (key[0] & mask) >> ((msb % 32) - (2 * level))
   
        
    return (key[1] & mask) >> (msb - (2 * level))
       
    
    

   
    # pass
    

def msb_index(n):
    if n == 0:
        return -1
    
    msb = 0
    while n != 1:
        n >>= 1
        msb += 1
    return msb

def get_MSB(key):
    a = msb_index(key[0])
    b = msb_index(key[1])

    if a >= 0:
        return a + 32
    
    if b >= 0:
        return b
    
    return -1;
    
def get_depth(key):
    


    pass


    

# key_msb = 0b0111_0011_0011_0011_1010_0101_0000_1001
            
# key_msb = 0b0000_0000_0001_1010_1010_1010_1010_0101
key_msb = 0b0000_0000_0000_0000_0000_0000_0000_0000
key_lsb = 0b0000_0000_0000_0000_0000_0000_0001_1111

# key_msb = 0b0111_1111_1111_1111_1111_1111_1111_1111
# key_lsb = 0b1111_1111_1111_1111_1111_1111_1111_1111

key = [key_msb, key_lsb]


# def leaf_space_to_quad_space(key):
#     msb = get_MSB(key)
#     transformation = [0, 0]
#     temp = [0, 0]
#     scale = 1.0

#     for i in range(msb // 2):
#         current_branching = get_branching(key, i, msb)
#         print(current_branching)
#         temp = get_translation(current_branching >> 1)
#         temp[0] *= scale
#         temp[1] *= scale
#         rotation_index = get_rotation(current_branching, current_branching >> 1, current_branching & 0b01)
#         r = rotate(rotation_index, temp)
#         transformation[0] = r[0]
#         transformation[1] = r[1]
#         scale *= 0.5
    
    # print(transformation, scale, rotation_index)

# a = ""
# key = [0, 0b010111]
# msb = get_MSB(key)
# for i in range((msb) // 2):
#     a += str(get_branching(key, i, msb - 2))

# print(quick_sin(0), "=>", 0)
# print(quick_sin(1), "=>", 1)
# print(quick_sin(2), "=>", 0)
# print(quick_sin(3), "=>", -1)
# print(quick_sin(4), "=>", 0)
# print(quick_sin(5), "=>", 1)
# print(quick_sin(6), "=>", 0)
# print(quick_sin(7), "=>", -1)
# # print(a)

# def custom_transform(input_bits):
#     # Define the correct transformation using bitwise operations
#     output_bits = ((input_bits >> 1) & 0b1) | ((input_bits & 0b1) << 1)
    
#     return output_bits

# # Test cases
# test_cases = [0b00, 0b01, 0b10, 0b11]

# for input_bits in test_cases:
#     output_bits = custom_transform(input_bits)
#     print(f"{input_bits:02b} -> {output_bits:02b}")

