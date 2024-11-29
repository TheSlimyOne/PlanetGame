import math

def number_to_spaced_binary(number):
    # Convert the number to binary and remove the '0b' prefix
    binary_string = bin(number)[2:]
    # Ensure the binary string has an even length
    if len(binary_string) % 2 != 0:
        binary_string = '0' + binary_string
    # Add spaces between every two digits
    spaced_binary = ' '.join(binary_string[i:i+2] for i in range(0, len(binary_string), 2))
    return spaced_binary

lod = 16
start = int(math.pow(4, lod + 1))
end = 2 * start
print(end - start)
# for i in range(start, end):
#     print(number_to_spaced_binary(i))

