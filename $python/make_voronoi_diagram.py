import random
import plotly.graph_objects as go
import math
from collections import defaultdict as dd
import numpy as np

bounds = (1920*2, 1080*2)


def generate_random_seeds(num_points, xlim, ylim, isInt=False):
    points = []

    func = random.randint if isInt else random.uniform

    for _ in range(num_points):
        x_coord = round(func(1, xlim), 3)
        y_coord = round(func(1, ylim), 3)
        points.append((x_coord, y_coord))
    return points

def draw_circles(circles, fig, num_points=100, color="gray"):
    theta = np.linspace(0, 2 * np.pi, num_points)
    for circle in circles:
        center = circle[0]
        radius = circle[1]
        x_circle = center[0] + radius * np.cos(theta)
        y_circle = center[1] + radius * np.sin(theta)
        circle_trace = go.Scatter(x=x_circle, y=y_circle, mode='lines', line=dict(color=color, width=1))
        draw_points([center], fig, color=color)
        fig.add_trace(circle_trace)

def get_triangle_from_edge(triangle_edges, edge_index):
    
    edge_a = None
    edge_b = None
    edge_c = None

    if (edge_index % 3 == 0):
        edge_a = triangle_edges[edge_index + 0]["vertices"]
        edge_b = triangle_edges[edge_index + 1]["vertices"]
        edge_c = triangle_edges[edge_index + 2]["vertices"]
    elif (edge_index % 3 == 1):
        edge_a = triangle_edges[edge_index - 1]["vertices"]
        edge_b = triangle_edges[edge_index + 0]["vertices"]
        edge_c = triangle_edges[edge_index + 1]["vertices"]
    else:
        edge_a = triangle_edges[edge_index - 2]["vertices"]
        edge_b = triangle_edges[edge_index - 1]["vertices"]
        edge_c = triangle_edges[edge_index + 0]["vertices"]
  
    return (edge_a, edge_b, edge_c)

def find_circle(point1, point2, point3):
    x1, y1 = point1
    x2, y2 = point2
    x3, y3 = point3

    d = 2 * (x1 * (y2 - y3) + x2 * (y3 - y1) + x3 * (y1 - y2))
    h = ((x1**2 + y1**2) * (y2 - y3) + (x2**2 + y2**2) * (y3 - y1) + (x3**2 + y3**2) * (y1 - y2)) / (d if d != 0 else 1)
    k = ((x1**2 + y1**2) * (x3 - x2) + (x2**2 + y2**2) * (x1 - x3) + (x3**2 + y3**2) * (x2 - x1)) / (d if d != 0 else 1)

    radius = math.sqrt((x1 - h) ** 2 + (y1 - k) ** 2)
    return (round(h, 2), round(k, 2)), round(radius, 2)

def draw_points(points, fig, color="red"):
    for point in points:
        trace = go.Scatter(x=[point[0]], y=[point[1]], mode='markers', marker=dict(color=color))
        fig.add_trace(trace)

def draw_edges(edges, fig):
    for edge in edges:
        vertices = edge['vertices']
        x_values = [vertex[0] for vertex in vertices]
        y_values = [vertex[1] for vertex in vertices]


        trace = go.Scatter(
            x=x_values,
            y=y_values,
            mode='lines', 
            line=dict(color=edge['color']))
        fig.add_trace(trace)

def bound_point(point, bounds):
    bounded_point = [point[0], point[1]]
    if point[0] < 0:
        bounded_point[0] = 0
    elif point[0] > bounds[0]:
        bounded_point[0] = bounds[0]

    if point[1] < 0:
        bounded_point[1] = 0
    elif point[1] > bounds[1]:
        bounded_point[1] = bounds[1]
    return bounded_point

def bowyer_watson(seeds):
    inital_vertices = ((0, 0), (0, bounds[1]), (bounds[0], 0), (bounds[0], bounds[1]))


    fig = go.Figure()
    fig.update_xaxes(range=[-bounds[0] * 2, bounds[0] * 2])
    fig.update_yaxes(range=[-bounds[1] * 2, bounds[1] * 2])
    
    triangle_edges = [
        {'vertices': (inital_vertices[0], inital_vertices[2]), 'color': 'orange'},
        {'vertices': (inital_vertices[2], inital_vertices[1]), 'color': 'orange'},
        {'vertices': (inital_vertices[1], inital_vertices[0]), 'color': 'orange'},

        {'vertices': (inital_vertices[2], inital_vertices[3]), 'color': 'orange'},
        {'vertices': (inital_vertices[3], inital_vertices[1]), 'color': 'orange'},
        {'vertices': (inital_vertices[1], inital_vertices[2]), 'color': 'orange'},
    ]

   

    for seed in seeds:
        fig.data = []
        bad_triangles_edges =  []
        good_triangles_edges = []

        for i in range(0, len(triangle_edges), 3):
            edges = (
                triangle_edges[i+0]["vertices"], 
                triangle_edges[i+1]["vertices"],
                triangle_edges[i+2]["vertices"]
            )

            vertices = (edges[0][0], edges[1][0], edges[2][0])
        
            center, radius = find_circle(vertices[0], vertices[1], vertices[2])
          
            if math.sqrt((center[0] - seed[0])**2 + (center[1] - seed[1])**2) <= radius:     
                bad_triangles_edges.append(edges[0])
                bad_triangles_edges.append(edges[1])
                bad_triangles_edges.append(edges[2])
            else: 
                good_triangles_edges.append(triangle_edges[i+0])
                good_triangles_edges.append(triangle_edges[i+1])
                good_triangles_edges.append(triangle_edges[i+2])

        for a, b in bad_triangles_edges:
            if (b, a) not in bad_triangles_edges:
                good_triangles_edges.append({'vertices': (a, b), 'color': 'red'})
                good_triangles_edges.append({'vertices': (b, seed), 'color': 'red'})
                good_triangles_edges.append({'vertices': (seed, a), 'color': 'red'})
        
          
        triangle_edges = good_triangles_edges

    voronoi_edge = []
    voronoi_vertices = []

    for edge_index in range(len(triangle_edges)):

        a, b = triangle_edges[edge_index]["vertices"]

        other_edge_index = [i for i, j in enumerate(triangle_edges) if j["vertices"] == (b, a)]
        
        if other_edge_index != []:
            other_edge_index = other_edge_index[0]

            triangle_a = get_triangle_from_edge(triangle_edges, edge_index)
            triangle_b = get_triangle_from_edge(triangle_edges, other_edge_index)
  

            vertices_a = (triangle_a[0][0], triangle_a[1][0], triangle_a[2][0])
            vertices_b = (triangle_b[0][0], triangle_b[1][0], triangle_b[2][0])

            center_a, _ = find_circle(vertices_a[0], vertices_a[1], vertices_a[2])
            center_b, _ = find_circle(vertices_b[0], vertices_b[1], vertices_b[2])

            # inital_vertices = ((0, 0), (0, bounds[1]), (bounds[0], 0), (bounds[0], bounds[1]))
            
            # bounded_a = bound_point(center_a, bounds)
            # bounded_b = bound_point(center_b, bounds)

            voronoi_vertices.append(center_a)
            voronoi_edge.append({'vertices': (center_a, center_b), 'color': 'red'})

            


    draw_edges(voronoi_edge, fig)
    draw_points(voronoi_vertices, fig, "green")
    draw_points(seeds, fig, color="blue")
    fig.show()



if __name__ == "__main__":
    seeds_amount = 250
    seeds = generate_random_seeds(seeds_amount, bounds[0]//2, bounds[1]//2, isInt=True)
    data = bowyer_watson(seeds)

