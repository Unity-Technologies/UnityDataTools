CREATE INDEX refs_object_index ON refs(object);
CREATE INDEX refs_referenced_object_index ON refs(referenced_object);
CREATE INDEX managed_references_object_index ON managed_references(object);
