import random
import math
from collections import defaultdict as dd
import numpy as np
from mpl_toolkits.mplot3d import Axes3D
from skspatial.objects import Sphere
import plotly.graph_objects as go
from collections import defaultdict as dd
random.seed()

def generate_random_seeds(num_points, planet_radius):
    points = []

    for _ in range(num_points):
        a = random.uniform(0, 1)
        long = math.acos(2 * a - 1)
        lat = random.uniform(-180, 180)

        x = planet_radius * math.cos(lat) * math.cos(long)
        y = planet_radius * math.cos(lat) * math.sin(long)
        z = planet_radius * math.sin(lat)
                
        points.append((round(x, 2), round(y, 2), round(z, 2)))
    points.sort(key=lambda x:x[0])
        
    return points


def generate_sphere(planet_radius):
    return Sphere([0,0,0], planet_radius)


def draw_edges(edges, fig):
    x_verts = []
    y_verts = []
    z_verts = []

    i_tri = []
    j_tri = []
    k_tri = []
    
    for i in range(0, len(edges), 12):
        this_tetrahedron = (
            edges[i]['vertices'][0],
            edges[i+1]['vertices'][0],
            edges[i+2]['vertices'][0],
            edges[i+3]['vertices'][0],
            edges[i+4]['vertices'][0],
            edges[i+5]['vertices'][0],
            edges[i+6]['vertices'][0],
            edges[i+7]['vertices'][0],
            edges[i+8]['vertices'][0],
            edges[i+9]['vertices'][0],
            edges[i+10]['vertices'][0],
            edges[i+11]['vertices'][0]
        )

        

        for vert in this_tetrahedron:
            x_verts.append(vert[0])
            y_verts.append(vert[1])
            z_verts.append(vert[2])

        i_tri += [0 + i, 3 + i, 6 + i, 9 + i]
        j_tri += [1 + i, 4 + i, 7 + i, 10 + i]
        k_tri += [2 + i, 5 + i, 8 + i, 11 + i]

    trace = go.Mesh3d(
            # 8 vertices of a cube
            x=x_verts,
            y=y_verts,
            z=z_verts,
            colorbar_title='z',
            colorscale=[[0, 'gold'],
                        [0.5, 'mediumturquoise'],
                        [1, 'magenta']],
            # Intensity of each vertex, which will be interpolated and color-coded
            intensity = np.linspace(0, 1, 8, endpoint=True),
            # i, j and k give the vertices of triangles
            i = i_tri,
            j = j_tri,
            k = k_tri,
            name='y',
            showscale=True
        )
    fig.add_trace(trace)

def draw_points(points, fig, color="red"):
    for point in points:
        trace = go.Scatter3d(x=[point[0]], y=[point[1]], z=[point[2]], mode='markers', marker=dict(color=color))
        fig.add_trace(trace)

def merge(h_1, h_2):
    pass

def subtract_vectors(vector_a, vector_b):
    return [a - b for a, b in zip(vector_a, vector_b)]

def calculate_centroid(vectors):
    return tuple([round(sum(x)/len(vectors), 2) for x in zip(*vectors)])

def calculate_magnitude(vector):
    return sum(component**2 for component in vector)**0.5

def normalize_vector(vector):
    magnitude = calculate_magnitude(vector)
    normalized_vector = [component / magnitude for component in vector]
    return normalized_vector

def dot_product(vector1, vector2):
    return sum(a * b for a, b in zip(vector1, vector2))

def cross_product(vector1, vector2):
    x = vector1[1] * vector2[2] - vector1[2] * vector2[1]
    y = vector1[2] * vector2[0] - vector1[0] * vector2[2]
    z = vector1[0] * vector2[1] - vector1[1] * vector2[0]
    return [x, y, z]

def multiply_vector_by_scale(scale, vector):
    return [scale * element for element in vector]

def calculate_normal(vertex1, vertex2, vertex3):
    vector1 = subtract_vectors(vertex2, vertex1)
    vector2 = subtract_vectors(vertex3, vertex1)
    
    normal = cross_product(vector1, vector2)
    magnitude = calculate_magnitude(normal)
    
    normalized_normal = [component / magnitude for component in normal]
    return normalized_normal



def is_point_in_front_of_triangle(point, triangle):

    triangle = (triangle[0][0], triangle[1][0], triangle[2][0])
    normal = calculate_normal(*triangle)
    results = dot_product(normalize_vector(normal), normalize_vector(subtract_vectors(point, triangle[0])))

    if results > 0:
        return True
    elif results < 0:
        return False
    else:
        print("warning plannar point")
        return False
    
def orientate_initial_edges(triangle, tetrahedron_centroid):
    triangle = (triangle[0][0], triangle[1][0], triangle[2][0])
    normal = calculate_normal(*triangle)

    if dot_product(normalize_vector(normal), normalize_vector(subtract_vectors(tetrahedron_centroid, triangle[0]))) > 0:
        return (2, 1, 0)
    
    else:
        return (0, 1, 2)
    
def get_vertices(triangle):
    return (triangle[0][0], triangle[1][0], triangle[2][0])


def get_edges(vertices):
    return ((vertices[0], vertices[1]), (vertices[1], vertices[2]), (vertices[2], vertices[0]))

def generate_edges(point, edge):
    return (edge[1], point), (point, edge[0])
    
def is_coplannar(triangle_1, triangle_2):
    normal_1 = calculate_normal(*triangle_1)
    normal_2 = calculate_normal(*triangle_2)

    if not are_vectors_parallel(normal_1, normal_2):
        return False
    
    if dot_product(normal_2, subtract_vectors(triangle_1[0], triangle_2[0])) == 0:
        return True
    
    return False

def get_triangle_from_edge_index(index):
    return index - index % 3

def are_vectors_parallel(v1, v2):
    # Check if the cross product of v1 and v2 is the zero vector
    cross = cross_product(v1, v2)
    return all(coord == 0 for coord in cross)

def incremental(seeds):
    # Could cause issues if the four points are on the same line or plane
    inital_vertices = (seeds.pop(0), seeds.pop(0), seeds.pop(0), seeds.pop(0))
    tetrahedron_edges = [
        (inital_vertices[0], inital_vertices[1]),
        (inital_vertices[1], inital_vertices[2]),
        (inital_vertices[2], inital_vertices[0]),

        (inital_vertices[0], inital_vertices[2]),
        (inital_vertices[2], inital_vertices[3]),
        (inital_vertices[3], inital_vertices[0]),

        (inital_vertices[0], inital_vertices[3]),
        (inital_vertices[3], inital_vertices[1]),
        (inital_vertices[1], inital_vertices[0]),

        (inital_vertices[2], inital_vertices[1]),
        (inital_vertices[1], inital_vertices[3]),
        (inital_vertices[3], inital_vertices[2]),
    ]

    tetrahedron_centroid = calculate_centroid(inital_vertices)

    
    # print(tetrahedron_centroid)
    point_to_facets = dd(list)
    facets_to_point = dd(list)
    for i in range(0, len(tetrahedron_edges), 3):
        triangle = (tetrahedron_edges[i + 0], tetrahedron_edges[i + 1], tetrahedron_edges[i + 2])
        correct_triangle_indices = orientate_initial_edges(triangle, tetrahedron_centroid)
        tetrahedron_edges[correct_triangle_indices[0] + i] = triangle[0]
        tetrahedron_edges[correct_triangle_indices[1] + i] = triangle[1]
        tetrahedron_edges[correct_triangle_indices[2] + i] = triangle[2]
        triangle = (tetrahedron_edges[i + 0], tetrahedron_edges[i + 1], tetrahedron_edges[i + 2])
        facets_to_point[triangle] = []
        # print(f"triangle{get_vertices(triangle)}")

        for seed in seeds:
            if is_point_in_front_of_triangle(seed, triangle):
                point_to_facets[seed].append(triangle)
                facets_to_point[triangle].append(seed)
    # print("=======================")
    # [print(x) for x in tetrahedron_edges]
    # print("=======================")
    for seed in seeds:
        if point_to_facets[seed] != []:
            triangles = point_to_facets[seed]
            del(point_to_facets[seed])
            new_triangles = []
            for triangle in triangles:
                # Remove the triangle from the tetrahedron
                for edge in triangle:
                    a, b = generate_edges(seed, edge)
                    new_triangle = (edge, a, b)
                    facets_to_point[new_triangle] = []

                    this_edge_start_index = tetrahedron_edges.index(triangle[0])

                    corosponding_edge_index = tetrahedron_edges.index(edge[::-1])
                    corosponding_triangle_start = get_triangle_from_edge_index(corosponding_edge_index)
                    corosponding_triangle = (tetrahedron_edges[corosponding_triangle_start + 0], tetrahedron_edges[corosponding_triangle_start + 1], tetrahedron_edges[corosponding_triangle_start + 2])

                    new_triangles.append(new_triangle)
                    # # tetrahedron[this_edge_start_index]
                    # if is_coplannar(get_vertices(new_triangle), get_vertices(corosponding_triangle)):
                    #     facets_to_point[new_triangle] = facets_to_point[corosponding_triangle]
                    # else:
                    #     points = facets_to_point[triangle] + facets_to_point[corosponding_triangle]

                    #     for point in points:
                    #         if is_point_in_front_of_triangle(point, new_triangle):
                    #             point_to_facets[point].append(new_triangle)
                    #             facets_to_point[new_triangle].append(point)
                    # # edge_index = tetrahedron_edges.index(edge)
                  
                    # # print(edge, " ", corosponding_edge)
                    # print(edge_index, corosponding_edge_index)
            print(new_triangles)
                # tetrahedron_edges.index(edge)


 

def split(seeds):
    half = len(seeds)//2
    return seeds[:half], seeds[half:]

x = float("inf")
def divide_and_conquer(seeds):
    if len(seeds) <= x:
        return incremental(seeds)
    
    (p_1, p_2) = split(seeds)
    h_1 = divide_and_conquer(p_1)
    h_2 = divide_and_conquer(p_2)
    return merge(h_1, h_2)

if __name__ == "__main__":
    fig = go.Figure()
    seed_amount = 8
    planet_radius = 20
    seeds = generate_random_seeds(seed_amount, planet_radius)
    convex_hull = divide_and_conquer(seeds)
    # draw_edges(convex_hull, fig)
    # fig.show()



