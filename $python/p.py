import random
import math
import plotly.graph_objects as go
import numpy as np


def generate_sphere_vertices(radius=1, n_latitude=20, n_longitude=20):
    latitudes = np.linspace(0, np.pi, n_latitude)
    longitudes = np.linspace(0, 2 * np.pi, n_longitude)

    latitudes, longitudes = np.meshgrid(latitudes, longitudes)

    x = radius * np.sin(latitudes) * np.cos(longitudes)
    y = radius * np.sin(latitudes) * np.sin(longitudes)
    z = radius * np.cos(latitudes)

    return go.Scatter3d(x=x.flatten(), y=y.flatten(), z=z.flatten(), mode='markers')


def generate_sphere_points(radius, num_points=100):
    points = []
    
    for i in range(num_points):
        theta = 2 * math.pi * i / (num_points - 1)
        phi = math.pi * i / (num_points - 1)

        x = radius * math.sin(phi) * math.cos(theta)
        y = radius * math.sin(phi) * math.sin(theta)
        z = radius * math.cos(phi)

        points.append((x, y, z))

    return points


def generate_random_sphere_points(amount_of_points, radius):
    points = []

    for _ in range(amount_of_points):
        # Generate random spherical coordinates
        theta = random.uniform(0, 2 * math.pi)  # azimuthal angle
        phi = random.uniform(0, math.pi)  # polar angle

        # Convert spherical coordinates to Cartesian coordinates
        x_coord = round(radius * math.sin(phi) * math.cos(theta), 3)
        y_coord = round(radius * math.sin(phi) * math.sin(theta), 3)
        z_coord = round(radius * math.cos(phi), 3)

        points.append((x_coord, y_coord, z_coord))

    return points


def draw_points(coords, color='blue'):
    x_coords, y_coords, z_coords = zip(*coords)
    points = go.Scatter3d(
        x=x_coords,
        y=y_coords,
        z=z_coords,
        mode='markers',
        marker=dict(size=8, color=color),
    )

    return points


def draw_triangles(triangle_edges):
    trace = []
    for i in range(0, len(triangle_edges), 3):
        edge_a = triangle_edges[i + 0]['vertices']
        edge_b = triangle_edges[i + 1]['vertices']
        edge_c = triangle_edges[i + 2]['vertices']



        x = (edge_a[0][0], edge_b[0][0], edge_c[0][0])
        y = (edge_a[0][1], edge_b[0][1], edge_c[0][1])
        z = (edge_a[0][2], edge_b[0][2], edge_c[0][2])


        trace.append(go.Mesh3d(
            x=x,
            y=y,
            z=z,
            i=[2],
            j=[0],
            k=[1],
            opacity=0.5,
            color="yellow"
            )
        )
    return trace


def draw_edges(edges, color='red'):
    x_coords, y_coords, z_coords = [], [], []

    for edge in edges:
        for i in range(2):
            x_coords.append(edge['vertices'][i][0]) 
            y_coords.append(edge['vertices'][i][1]) 
            z_coords.append(edge['vertices'][i][2])
  
    trace = go.Scatter3d(
        x=x_coords,
        y=y_coords,
        z=z_coords,
        mode='lines', 
        line=dict(color=color))
    return trace

def normalize(vector):
    magnitude = sum(x ** 2 for x in vector) ** 0.5
    normalized_vector = [x / magnitude for x in vector]
    return normalized_vector


# def plot_xyz_points_with_sphere(points, radius=1.0):

#     initial_vertices = (points[0], points[1], points[2], points[3])

    

#     triangle_edges = [
#         {'vertices': (initial_vertices[0], initial_vertices[2])},
#         {'vertices': (initial_vertices[2], initial_vertices[1])},
#         {'vertices': (initial_vertices[1], initial_vertices[0])},

#         {'vertices': (initial_vertices[2], initial_vertices[3])},
#         {'vertices': (initial_vertices[3], initial_vertices[1])},
#         {'vertices': (initial_vertices[1], initial_vertices[2])},

#         {'vertices': (initial_vertices[0], initial_vertices[3])},
#         {'vertices': (initial_vertices[3], initial_vertices[2])},
#         {'vertices': (initial_vertices[2], initial_vertices[0])},

#         {'vertices': (initial_vertices[1], initial_vertices[3])},     
#         {'vertices': (initial_vertices[3], initial_vertices[2])},
#         {'vertices': (initial_vertices[2], initial_vertices[1])},     
#     ]

#     for i in range(0, len(triangle_edges), 3):
        

    
#     fig = go.Figure(data=[draw_edges(triangle_edges, color='red'), draw_points(points, color='blue'), *draw_triangles(triangle_edges)])
    

#     fig.update_layout(scene=dict(
#         xaxis_title='X-axis',
#         yaxis_title='Y-axis',
#         zaxis_title='Z-axis',
#     ))

#     fig.show()

if __name__ == '__main__':
    # plot_xyz_points_with_sphere(generate_random_sphere_points(50, 1))
    pass