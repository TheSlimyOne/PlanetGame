offset = 1
kernel = [
    0, 
    0.25, 
    0, 
    0.25, 
    0.5, 
    0.25, 
    0, 
    0.25, 
    0]
s = ""
for i in range(-offset, offset + 1):
    for j in range(-offset, offset + 1):
        kernel_index = (i + offset) * (2 * offset + 1) + (j + offset)
        s += f"|{kernel_index}"
    s+= "\n"

print(s)