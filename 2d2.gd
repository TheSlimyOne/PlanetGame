extends MultiMeshInstance3D


func _ready():
	print(Basis())
	for x in range(20):
		for z in range(20):
			pass
			#self.multimesh.set_instance_transform(z * 20 + x, Transform3D(Basis(), Vector3(x, 0.0, -z)))
			#var newTrans = multimesh.get_instance_transform(z * 20 + x)
			#print(newTrans, " ", "GD")
